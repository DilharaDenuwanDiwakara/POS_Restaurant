using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Services;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class RoomRegisterViewModel : BaseViewModel
    {
        private static readonly string[] RoomFormProperties =
        {
            nameof(RoomName),
            nameof(RoomCapacity),
            nameof(RoomFloorNumber)
        };

        private readonly IRoomBookingRepository _roomBookingRepository;
        private readonly CloudStorageService _storageService;
        private string _localImageToUploadPath;
        private string _roomImageObjectKey;
        private IUserSessionService _userSessionService;

        public RoomRegisterViewModel(IRoomBookingRepository roomBookingRepository, CloudStorageService storageService, IUserSessionService userSessionService)
        {
            _roomBookingRepository = roomBookingRepository ?? throw new ArgumentNullException(nameof(roomBookingRepository));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SaveRoomCommand = new AsyncRelayCommand(async _ => await SaveRoomAsync(), _ => CanSaveRoom);
            EditRoomCommand = new RelayCommand(
                execute: _ => ExecuteEditRoomCommand(),
                canExecute: _ => SelectedRoom != null && CanEditRoom // Ensures the button is disabled if they lack permission
            );
            RefreshRoomsCommand = new AsyncRelayCommand(async _ => await LoadRoomsAsync());
            UploadImageCommand = new RelayCommand(_ => BrowseImage());

            _ = InitializeAsync();
        }

        #region Permissions
        public bool CanEditRoom => _userSessionService.HasPermission("RESTAURANT_ROOM_REGISTER_EDIT");
        #endregion

        #region Properties
        public ObservableCollection<Room> RoomList { get; } = new ObservableCollection<Room>();
        public RoomType[] RoomTypes { get; } = (RoomType[])Enum.GetValues(typeof(RoomType));
        public RoomStatus[] RoomStatuses { get; } = (RoomStatus[])Enum.GetValues(typeof(RoomStatus));

        private Room _selectedRoom;
        public Room SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (SetProperty(ref _selectedRoom, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _roomName;
        public string RoomName
        {
            get => _roomName;
            set
            {
                SetProperty(ref _roomName, value);
                ValidateRoomName();
                RaiseCanExecuteChanged();
            }
        }

        private int _roomCapacity = 1;
        public int RoomCapacity
        {
            get => _roomCapacity;
            set
            {
                SetProperty(ref _roomCapacity, value);
                ValidateRoomCapacity();
                RaiseCanExecuteChanged();
            }
        }

        private RoomType _roomType = RoomType.MEETING;
        public RoomType RoomType
        {
            get => _roomType;
            set
            {
                SetProperty(ref _roomType, value);
                RaiseCanExecuteChanged();
            }
        }

        private bool _roomIsActive = true;
        public bool RoomIsActive
        {
            get => _roomIsActive;
            set => SetProperty(ref _roomIsActive, value);
        }

        private RoomStatus _roomStatus = RoomStatus.AVAILABLE;
        public RoomStatus RoomStatus
        {
            get => _roomStatus;
            set
            {
                SetProperty(ref _roomStatus, value);
                RaiseCanExecuteChanged();
            }
        }

        private string _roomDescription;
        public string RoomDescription
        {
            get => _roomDescription;
            set => SetProperty(ref _roomDescription, value);
        }

        private int _roomFloorNumber = 1;
        public int RoomFloorNumber
        {
            get => _roomFloorNumber;
            set
            {
                SetProperty(ref _roomFloorNumber, value);
                ValidateRoomFloorNumber();
                RaiseCanExecuteChanged();
            }
        }

        private string _roomImageUrl;
        public string RoomImageUrl
        {
            get => _roomImageUrl;
            set
            {
                SetProperty(ref _roomImageUrl, value);
                RaiseCanExecuteChanged();
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";
        #endregion

        #region Commands
        public ICommand SaveRoomCommand { get; }
        public ICommand EditRoomCommand { get; }
        public ICommand RefreshRoomsCommand { get; }
        public ICommand UploadImageCommand { get; }
        #endregion

        #region Methods
        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            CreateNewRoom();
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                var rooms = await _roomBookingRepository.GetAllRoomsAsync();

                RoomList.Clear();
                foreach (var room in rooms)
                {
                    RoomList.Add(room);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rooms: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool CanSaveRoom =>
            !HasValidationErrorsFor(RoomFormProperties) &&
            !string.IsNullOrWhiteSpace(RoomName) &&
            RoomCapacity > 0 &&
            RoomFloorNumber > 0;
        private async Task SaveRoomAsync()
        {
            ValidateRoomForm();

            if (HasValidationErrorsFor(RoomFormProperties))
            {
                MessageBox.Show("Please correct room form validation errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (IsEditing && !CanEditRoom)
            {
                MessageBox.Show("You do not have permission to save room modifications.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                if (!string.IsNullOrEmpty(_localImageToUploadPath) && File.Exists(_localImageToUploadPath))
                {
                    string fileName = Path.GetFileName(_localImageToUploadPath);
                    _roomImageObjectKey = await _storageService.UploadFileAsync(_localImageToUploadPath, "room-images", fileName);
                    RoomImageUrl = _storageService.GetSecureFileUrl(_roomImageObjectKey);
                    _localImageToUploadPath = null;
                }

                if (IsEditing && SelectedRoom != null)
                {
                    SelectedRoom.Name = RoomName;
                    SelectedRoom.MaxCapacity = RoomCapacity;
                    SelectedRoom.Type = RoomType.ToString();
                    SelectedRoom.IsActive = RoomIsActive;
                    SelectedRoom.Status = RoomStatus.ToString();
                    SelectedRoom.Description = RoomDescription;
                    SelectedRoom.FloorNumber = RoomFloorNumber;
                    SelectedRoom.ImageUrl = _roomImageObjectKey;

                    await _roomBookingRepository.UpdateRoomAsync(SelectedRoom);
                    MessageBox.Show("Room updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var room = new Room
                    {
                        Name = RoomName,
                        MaxCapacity = RoomCapacity,
                        Type = RoomType.ToString(),
                        IsActive = RoomIsActive,
                        Status = RoomStatus.ToString(),
                        Description = RoomDescription,
                        FloorNumber = RoomFloorNumber,
                        ImageUrl = _roomImageObjectKey
                    };

                    await _roomBookingRepository.CreateRoomAsync(room);
                    MessageBox.Show("Room created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadRoomsAsync();
                CreateNewRoom();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper Method
        private void BrowseImage()
        {
            var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                _localImageToUploadPath = dlg.FileName;
                _roomImageObjectKey = null;
                RoomImageUrl = dlg.FileName;
            }
        }

        private void CreateNewRoom()
        {
            SelectedRoom = null;
            RoomName = string.Empty;
            RoomCapacity = 1;
            RoomType = default;
            RoomIsActive = true;
            RoomStatus = default;
            RoomDescription = string.Empty;
            RoomFloorNumber = 1;
            RoomImageUrl = null;
            _localImageToUploadPath = null;
            _roomImageObjectKey = null;
            IsEditing = false;

            ClearRoomErrors();
            RaiseCanExecuteChanged();
        }

        private void ExecuteEditRoomCommand()
        {
            if (!CanEditRoom)
            {
                MessageBox.Show("You do not have permission to edit room details.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetEditMode(true);
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (IsEditing && SelectedRoom != null)
            {
                RoomName = SelectedRoom.Name;
                RoomCapacity = SelectedRoom.MaxCapacity;
                RoomType = Enum.TryParse(SelectedRoom.Type, out RoomType parsedType) ? parsedType : default;
                RoomIsActive = SelectedRoom.IsActive;
                RoomStatus = Enum.TryParse(SelectedRoom.Status, out RoomStatus parsedStatus) ? parsedStatus : default;
                RoomDescription = SelectedRoom.Description;
                RoomFloorNumber = SelectedRoom.FloorNumber;
                _roomImageObjectKey = SelectedRoom.ImageUrl;
                RoomImageUrl = _storageService.GetSecureFileUrl(_roomImageObjectKey);
                _localImageToUploadPath = null;

                RaiseCanExecuteChanged();
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveRoomCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditRoomCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private bool HasValidationErrorsFor(IEnumerable<string> propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (GetErrors(propertyName) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidateRoomForm()
        {
            ValidateRoomName();
            ValidateRoomCapacity();
            ValidateRoomFloorNumber();
        }

        private void ValidateRoomName()
        {
            ClearErrors(nameof(RoomName));

            if (string.IsNullOrWhiteSpace(RoomName))
            {
                AddError(nameof(RoomName), "Room name is required.");
                return;
            }

            if (!Regex.IsMatch(RoomName.Trim(), @"^[a-zA-Z0-9\-\s]+$"))
            {
                AddError(nameof(RoomName), "Room name cannot contain special characters.");
            }
        }

        private void ValidateRoomCapacity()
        {
            ClearErrors(nameof(RoomCapacity));

            if (RoomCapacity <= 0)
            {
                AddError(nameof(RoomCapacity), "Capacity must be greater than 0.");
            }
        }

        private void ValidateRoomFloorNumber()
        {
            ClearErrors(nameof(RoomFloorNumber));

            if (RoomFloorNumber <= 0)
            {
                AddError(nameof(RoomFloorNumber), "Floor number must be greater than 0.");
            }
        }

        private void ClearRoomErrors()
        {
            foreach (var property in RoomFormProperties)
            {
                ClearErrors(property);
            }
        }
        #endregion
    }
}
