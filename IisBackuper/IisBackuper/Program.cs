// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using System.Xml;
using Microsoft.Web.Administration;

while (true)
{
    // backup e:\publish\iis_backup.json
    // restore e:\publish\iis_backup.json
    ExecuteCommand();
}

void ExecuteCommand()
{
    var readCommand = Console.ReadLine();
    var split = readCommand.Split(" ");
    if (split[0] == "backup")
    {
        if (split.Length < 2)
        {
            Console.WriteLine("need path");
            return;
        }
        var path = split[1];
        Backup(path);
    }
    else if (split[0] == "restore")
    {
        if (split.Length < 2)
        {
            Console.WriteLine("need path");
            return;
        }
        var path = split[1];
        Restore(path);
    }
    else
    {
        Console.WriteLine("backup path | restore path");
    }
}

void Backup(string path)
{
    using (var serverManager = new ServerManager())
    {
        var sites = serverManager.Sites.ToList();
        var pools = serverManager.ApplicationPools.ToList();

        var iisSites = new List<BackupSite>();
        foreach (var site in sites)
        {
            var sitePath = serverManager.Sites[site.Name].Applications["/"].VirtualDirectories["/"].PhysicalPath;
            var poolName = serverManager.Sites[site.Name].Applications["/"].ApplicationPoolName;
            var pool = pools.Single(x => x.Name == poolName);

            var backupSite = new BackupSite
            {
                Name = site.Name,
                PhysicalPath = sitePath,
                Bindings = site.Bindings.Select(x => new BackupSite.BackupBinding { Protocol = x.Protocol, BindingInformation = x.BindingInformation }).ToArray(),
                Pool = new BackupSite.BackupPool
                {
                    Name = pool.Name,
                    ManagedRuntimeVersion = pool.ManagedRuntimeVersion,
                    Enable32BitAppOnWin64 = pool.Enable32BitAppOnWin64,
                }
            };
            iisSites.Add(backupSite);
        }

        var saveData = JsonSerializer.Serialize(iisSites, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, saveData);
        Console.WriteLine("success");
    }
}

void Restore(string path)
{
    var data = File.ReadAllText(path);
    var iisData = JsonSerializer.Deserialize<List<BackupSite>>(data);
    using (var serverManager = new ServerManager())
    {
        foreach (var site in iisData)
        {
            try
            {
                ApplicationPool newPool = serverManager.ApplicationPools.Add(site.Pool.Name);
                newPool.Enable32BitAppOnWin64 = site.Pool.Enable32BitAppOnWin64;
                newPool.ManagedRuntimeVersion = site.Pool.ManagedRuntimeVersion;

                var newSite = serverManager.Sites.Add(site.Name, site.Bindings[0].Protocol, site.Bindings[0].BindingInformation, site.PhysicalPath);
                newSite.ApplicationDefaults.ApplicationPoolName = site.Pool.Name;
                newSite.ServerAutoStart = true;
                for (var i = 1; i < site.Bindings.Length; i++)
                {
                    newSite.Bindings.Add(site.Bindings[i].BindingInformation, site.Bindings[i].Protocol);
                }
                serverManager.CommitChanges();

                Console.WriteLine("success " + site.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine("error " + site);
                Console.WriteLine(ex.ToString());
            }
        }
    }
}

public class BackupSite
{
    public string Name { get; set; }
    public string PhysicalPath { get; set; }
    public BackupBinding[] Bindings { get; set; }
    public BackupPool Pool { get; set; }

    public class BackupBinding
    {
        public string Protocol { get; set; }
        public string BindingInformation { get; set; }
    }
    public class BackupPool
    {
        public string Name { get; set; }
        public string ManagedRuntimeVersion { get; set; }
        public bool Enable32BitAppOnWin64 { get; set; }
    }
}
