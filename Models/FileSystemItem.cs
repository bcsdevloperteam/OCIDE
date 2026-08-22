using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using OCIDE.Services;

namespace OCIDE.Models
{
    public class FileSystemItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsExpanded { get; set; }
        
        public object IconElement => IconManager.GetIcon(Name, IsDirectory);
        
        // Use an empty item initially to show the expansion arrow in UI for folders
        public ObservableCollection<FileSystemItem> Children { get; set; }

        public FileSystemItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
        }

        public void LoadChildren()
        {
            if (!IsDirectory) return;
            
            Children.Clear();
            
            try
            {
                var dirInfo = new DirectoryInfo(FullPath);
                
                // Add directories
                foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                {
                    // Skip hidden folders like .git or .ocide
                    if (dir.Name.StartsWith(".")) continue;

                    var item = new FileSystemItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true
                    };
                    item.Children.Add(new FileSystemItem()); // Dummy item for expansion
                    Children.Add(item);
                }

                // Add files
                foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
                {
                    Children.Add(new FileSystemItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false
                    });
                }
            }
            catch { /* Ignore unauthorized access */ }
        }
    }
}
