using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Serilog;

namespace AntiPrism.Core.Process;

public static class ProcessLister
{
    public static List<ProcessInfo> GetActiveProcesses()
    {
        var result = new List<ProcessInfo>();
        var systemProcesses = System.Diagnostics.Process.GetProcesses();

        foreach (var proc in systemProcesses)
        {
            try
            {
                var info = new ProcessInfo
                {
                    Id = proc.Id,
                    ProcessName = proc.ProcessName
                };

                try
                {
                    var mainModule = proc.MainModule;
                    if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                    {
                        info.ExecutablePath = mainModule.FileName;

                        try
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(info.ExecutablePath);
                            info.Description = !string.IsNullOrWhiteSpace(versionInfo.FileDescription)
                                ? versionInfo.FileDescription
                                : proc.ProcessName;
                            info.Publisher = versionInfo.CompanyName;
                        }
                        catch { }

                        // Extract digital certificate publisher if available
                        if (string.IsNullOrEmpty(info.Publisher) && File.Exists(info.ExecutablePath))
                        {
                            try
                            {
                                using var cert = X509Certificate.CreateFromSignedFile(info.ExecutablePath);
                                info.Publisher = cert.Subject;
                            }
                            catch { }
                        }
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Access Denied on protected/system process
                    info.IsSystemOrProtected = true;
                    info.Description = "System / Protected";
                }
                catch (InvalidOperationException)
                {
                    // Process exited
                    continue;
                }

                result.Add(info);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error inspecting process {Pid}", proc.Id);
            }
            finally
            {
                proc.Dispose();
            }
        }

        return result
            .OrderBy(p => p.IsSystemOrProtected)
            .ThenBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
