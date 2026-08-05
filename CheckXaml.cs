using System;
using System.IO;
using System.Xml;

class Program
{
    static void Main()
    {
        string dir = @"PointOfSale.UI";
        foreach (var file in Directory.GetFiles(dir, "*.xaml", SearchOption.AllDirectories))
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in {file}: {ex.Message}");
            }
        }
    }
}
