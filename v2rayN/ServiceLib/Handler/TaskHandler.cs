namespace ServiceLib.Handler
{
    public class TaskHandler
    {
        private static readonly Lazy<TaskHandler> _instance = new(() => new());
        public static TaskHandler Instance => _instance.Value;

        public void RegUpdateTask(Config config, Action<bool, string> updateFunc)
        {
            Task.Run(() => UpdateTaskRunSubscription(config, updateFunc));
            Task.Run(() => UpdateTaskRunGeo(config, updateFunc));
            Task.Run(() => UpdateTaskRunCore(config, updateFunc));
            Task.Run(() => UpdateTaskRunGui(config, updateFunc));
            Task.Run(() => ScheduledTasks(config, updateFunc));
        }

        private async Task ScheduledTasks(Config config, Action<bool, string> updateFunc)
        {
            Logging.SaveLog("Setup Scheduled Tasks");

            var numOfExecuted = 1;
            while (true)
            {
                //1 minute
                await Task.Delay(1000 * 60);

                //Execute once 1 minute
                await UpdateTaskRunSubscription(config, updateFunc);

                //Execute once 20 minute
                if (numOfExecuted % 20 == 0)
                {
                    //Logging.SaveLog("Execute save config");

                    await ConfigHandler.SaveConfig(config);
                    await ProfileExHandler.Instance.SaveTo();
                }

                //Execute once 1 hour
                if (numOfExecuted % 60 == 0)
                {
                    //Logging.SaveLog("Execute delete expired files");

                    FileManager.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1));
                    FileManager.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                    FileManager.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));

                    //Check once 1 hour
                    await UpdateTaskRunGeo(config, numOfExecuted / 60, updateFunc);
                }

                numOfExecuted++;
            }
        }

        private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> updateFunc)
        {
            var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
            var lstSubs = (await AppHandler.Instance.SubItems())?
                .Where(t => t.AutoUpdateInterval > 0)
                .Where(t => updateTime - t.UpdateTime >= t.AutoUpdateInterval * 60)
                .ToList();

            if (lstSubs is not { Count: > 0 })
            {
                return;
            }

            Logging.SaveLog("Execute update subscription");
            var updateHandle = new UpdateService();

            foreach (var item in lstSubs)
            {
                await updateHandle.UpdateSubscriptionProcess(config, item.Id, true, (bool success, string msg) =>
                {
                    updateFunc?.Invoke(success, msg);
                    if (success)
                    {
                        Logging.SaveLog($"Update subscription end. {msg}");
                    }
                });
                item.UpdateTime = updateTime;
                await ConfigHandler.AddSubItem(config, item);
                await Task.Delay(1000);
            }
        }

        private async Task UpdateTaskRunGeo(Config config, int hours, Action<bool, string> updateFunc)
        {
            var autoUpdateGeoTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunGeo");

            var updateHandle = new UpdateService();
            while (true)
            if (config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % config.GuiItem.AutoUpdateInterval == 0)
            {
                Logging.SaveLog("Execute update geo files");

                var updateHandle = new UpdateService();
                await updateHandle.UpdateGeoFileAll(config, (bool success, string msg) =>
                {
                    updateFunc?.Invoke(false, msg);
                });
            }
        }

        private async Task UpdateTaskRunCore(Config config, Action<bool, string> updateFunc)
        {
            var autoUpdateCoreTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunCore");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.GuiItem.AutoUpdateCoreInterval > 0)
                {
                    if ((dtNow - autoUpdateCoreTime).Hours % config.GuiItem.AutoUpdateCoreInterval == 0)
                    {
                        await updateHandle.CheckUpdateCore(ECoreType.Xray, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        await updateHandle.CheckUpdateCore(ECoreType.sing_box, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        await updateHandle.CheckUpdateCore(ECoreType.mihomo, config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);

                        autoUpdateCoreTime = dtNow;
                    }
                }
            }
        }

        private async Task UpdateTaskRunGui(Config config, Action<bool, string> updateFunc)
        {
            var autoUpdateGuiTime = DateTime.Now;

            Logging.SaveLog("UpdateTaskRunGui");

            var updateHandle = new UpdateService();
            while (true)
            {
                await Task.Delay(1000 * 3600);

                var dtNow = DateTime.Now;
                if (config.GuiItem.AutoUpdateCoreInterval > 0)
                {
                    if ((dtNow - autoUpdateGuiTime).Hours % config.GuiItem.AutoUpdateCoreInterval == 0)
                    {
                        await updateHandle.CheckUpdateGuiN(config, (bool success, string msg) =>
                        {
                            updateFunc?.Invoke(success, msg);
                        }, false);
                        autoUpdateGuiTime = dtNow;
                    }
                }
            }
        }
    }
}
