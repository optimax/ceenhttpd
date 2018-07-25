﻿using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Spawn
{
    public abstract class SpawnHandler : IAppDomainHandler
    {
        /// <summary>
        /// Path to the configuration file
        /// </summary>
        private readonly string m_path;

        /// <summary>
        /// The HTTP runner instance
        /// </summary>
        private InstanceRunner m_http_runner;
        /// <summary>
        /// The https runner instance
        /// </summary>
        private InstanceRunner m_https_runner;

        /// <summary>
        /// The task signalling stopped
        /// </summary>
        private readonly TaskCompletionSource<bool> m_stopped = new TaskCompletionSource<bool>();
        /// <summary>
        /// Gets a task that signals completion
        /// </summary>
        public Task StoppedAsync => m_stopped.Task;

        /// <summary>
        /// The storage creator
        /// </summary>
        private readonly IStorageCreator m_storage = new MemoryStorageCreator();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixSpawnHandler"/> class.
        /// </summary>
        /// <param name="path">The config file path.</param>
        public SpawnHandler(string path)
        {
            m_path = path;
        }

        protected abstract IWrappedRunner CreateRunner(string path, bool useSSL, IStorageCreator storage, System.Threading.CancellationToken token);

        /// <summary>
        /// Reload this instance.
        /// </summary>
        public async Task ReloadAsync(bool http, bool https)
        {
            var cfg = ConfigParser.ParseTextFile(m_path);
            var config = ConfigParser.CreateServerConfig(cfg);
            config.Storage = m_storage;

            ((MemoryStorageCreator)m_storage).ExpireCheckInterval = TimeSpan.FromSeconds(cfg.StorageExpirationCheckIntervalSeconds);

            var prevhttp = m_http_runner?.Wrapper;
            var prevhttps = m_https_runner?.Wrapper;

            IWrappedRunner new_http_runner = null;
            IWrappedRunner new_https_runner = null;

            try
            {
                if (http)
                    new_http_runner = CreateRunner(m_path, false, m_storage, default(System.Threading.CancellationToken));
                if (https)
                    new_https_runner = CreateRunner(m_path, true, m_storage, default(System.Threading.CancellationToken));
            }
            catch
            {
                if (new_http_runner != null)
                    try { new_http_runner.Kill(); }
                    catch { }

                if (new_https_runner != null)
                    try { new_https_runner.Kill(); }
                    catch { }

                throw;
            }

            m_http_runner = await ReplaceOrRestartAsync(m_http_runner, prevhttp, new_http_runner, cfg.HttpAddress, cfg.HttpPort, false, config);
            m_https_runner = await ReplaceOrRestartAsync(m_https_runner, prevhttps, new_https_runner, cfg.HttpsAddress, cfg.HttpsPort, true, config);



            if (new_https_runner == null)
            {
                if (m_https_runner != null)
                    m_http_runner.StopAsync();
            }
            else
            {
                if (m_https_runner == null)
                    m_https_runner = new InstanceRunner();

                m_https_runner.Wrapper = new_https_runner;
            }

            // TODO: If the runner is reconfigured, then restart it

            var dummy = m_http_runner?.RunnerTask.ContinueWith(x =>
            {
                if (!m_http_runner.ShouldStop && InstanceCrashed != null)
                    InstanceCrashed(cfg.HttpAddress, false, x.IsFaulted ? x.Exception : new Exception("Unexpected stop"));
            });
            dummy = m_https_runner?.RunnerTask.ContinueWith(x =>
            {
                if (!m_https_runner.ShouldStop && InstanceCrashed != null)
                    InstanceCrashed(cfg.HttpsAddress, true, x.IsFaulted ? x.Exception : new Exception("Unexpected stop"));
            });

            if (prevhttp != null || prevhttps != null)
            {
                await Task.Run(async () =>
                {
                    var t = Task.WhenAll(new[] { prevhttp?.StopAsync(), prevhttps?.StopAsync() }.Where(x => x != null));

                    // Give old processes time to terminate
                    var maxtries = cfg.MaxUnloadWaitSeconds;
                    while (maxtries-- > 0)
                    {
                        // All done, then quit
                        if (t.IsCompleted)
                            return;

                        await Task.Delay(1000);
                    }

                    if (prevhttp != null)
                        prevhttp.Kill();
                    if (prevhttps != null)
                        prevhttps.Kill();
                });
            }
        }

        private async Task<InstanceRunner> ReplaceOrRestartAsync(InstanceRunner runner, IWrappedRunner prevhandler, IWrappedRunner newhandler, string newaddr, int newport, bool usessl, ServerConfig config)
        {
            if (newhandler == null)
            {
                // We are stopping the handler

                if (runner != null)
                    runner.StopAsync();
                runner = null;
            }
            else
            {
                // We are starting, or restarting the handler

                if (runner == null)
                {
                    runner = new InstanceRunner();
                    runner.Wrapper = newhandler;
                    await runner.RestartAsync(newaddr, newport, usessl, config);
                }
                else
                {
                    // If any of these change, we need to restart the listen socket
                    if (runner.Address != newaddr || runner.Port != newport)
                    {
                        // Start the new instance first
                        var newrunner = new InstanceRunner();
                        newrunner.Wrapper = newhandler;
                        await newrunner.RestartAsync(newaddr, newport, usessl, config);

                        runner.StopAsync();
                        return newrunner;
                    }
                    // In this case we must restart the socket, but need to stop first
                    else if (config.SocketBacklog != runner.Config.SocketBacklog)
                    {
                        await runner.StopAsync();
                        runner = new InstanceRunner();
                        runner.Wrapper = newhandler;
                        await runner.RestartAsync(newaddr, newport, usessl, config);

                    }
                    else
                    {
                        // No changes, just apply the new handler for future requests
                        runner.Wrapper = newhandler;
                    }
                }
            }

            return runner;
        }

        public event Action<string, bool, Exception> InstanceCrashed;

        /// <summary>
        /// Stops the instance
        /// </summary>
        /// <returns>The async.</returns>
        public async Task StopAsync()
        {
            if (m_http_runner == null && m_https_runner == null)
                return;

            await Task.WhenAll(new Task[] { m_http_runner?.StopAsync(), m_https_runner?.StopAsync() }.Where(x => x != null));
            m_stopped.TrySetResult(true);
        }
    }
}