// SiteBlocker.Service/Worker.cs
using SiteBlocker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SiteBlocker.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly SiteBlocker.Core.SiteBlocker _blocker;
        private BlockerConfig _config;
        private DateTime _lastConfigCheck = DateTime.MinValue;
        
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _blocker = new SiteBlocker.Core.SiteBlocker();
            _config = BlockerConfig.LoadFromFile(BlockerConfig.DefaultConfigPath);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SiteBlocker Service starting at: {time}", DateTimeOffset.Now);
            
            // Sprawdź uprawnienia administratora
            if (!AdminHelper.IsRunningAsAdmin())
            {
                _logger.LogError("Service lacks administrator privileges");
                return;
            }
            
            // Włącz wszystkie metody blokowania
            _blocker.UseHostsFile = true;
            _blocker.UseFirewall = true;
            _blocker.UseWfp = true; // Włącz WFP w usłudze
            
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Sprawdź konfigurację co 5 sekund
                        if ((DateTime.Now - _lastConfigCheck).TotalSeconds >= 5)
                        {
                            _config = BlockerConfig.LoadFromFile(BlockerConfig.DefaultConfigPath);
                            _lastConfigCheck = DateTime.Now;
                            _logger.LogDebug("Configuration reloaded");
                        }
                        
                        // Sprawdź, czy powinniśmy blokować
                        bool shouldBlock = _config.ShouldBeActiveNow();
                        
                        if (shouldBlock && _config.BlockedSites.Count > 0)
                        {
                            _blocker.BlockSites(_config.BlockedSites);
                            _logger.LogInformation("Blocking sites: Active");
                        }
                        else if(!shouldBlock && _config.BlockedSites.Count > 0)
                        {
                            _blocker.UnblockSites();
                            _logger.LogInformation("Blocking sites: Inactive");
                        }
                        
                        await Task.Delay(1000, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in SiteBlocker service");
                        await Task.Delay(5000, stoppingToken); // Dłuższe opóźnienie przy błędzie
                    }
                }
            }
            finally
            {
                // Wyczyść blokady przy zatrzymaniu usługi
                _blocker.UnblockSites();
                _blocker.Dispose();
            }
        }
    }
}