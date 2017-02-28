using System;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.API.Resources;
using Slamby.Common.DI;
using Slamby.API.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using Slamby.API.Services;

namespace Slamby.API.Helpers
{
    [TransientDependency]
    public class ProcessHandler
    {
        readonly ProcessQuery processQuery;

        public IGlobalStoreManager GlobalStore { get; set; }

        public ProcessHandler(ProcessQuery processQuery, IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.processQuery = processQuery;
        }

        public ProcessElastic Create(ProcessTypeEnum type, string affectedObjectId, object initObject, string description)
        {
            var processId = Guid.NewGuid().ToString();
            var process = new ProcessElastic
            {
                Id = processId,
                Status = (int)ProcessStatusEnum.InProgress,
                Type = (int)type,
                Start = DateTime.UtcNow,
                Percent = 0.0,
                ErrorMessages = new System.Collections.Generic.List<string>(),
                InitObject = initObject,
                AffectedObjectId = affectedObjectId,
                Description = description,
                InstanceId = GlobalStore.InstanceId
            };
            processQuery.Index(process);
            return process;
        }

        public void Start(ProcessElastic process, Action<CancellationTokenSource> action)
        {
            var tokenSource = new CancellationTokenSource();
            var task = new Task(
                () => action(tokenSource),
                tokenSource.Token,
                TaskCreationOptions.LongRunning);
            task.Start();

            GlobalStore.Processes.Add(
                process.Id,
                new Models.GlobalStoreProcess()
                {
                    Process = process,
                    CancellationToken = tokenSource,
                    Task = task
                });
        }

        public void Changed(string processId, double percent)
        {
            //if it's already cancelled but another thread just finished right after that
            if (GlobalStore.Processes.Get(processId) == null) return;

            var process = GlobalStore.Processes.Get(processId).Process;
            process.Percent = percent;
            processQuery.Update(processId, process);
        }

        public void Finished(string processId, string resultMessage)
        {
            var process = GlobalStore.Processes.Get(processId).Process;

            process.Percent = 100;
            process.End = DateTime.UtcNow;
            process.Status = (int)ProcessStatusEnum.Finished;
            process.ResultMessage = resultMessage;

            GlobalStore.Processes.Remove(processId);

            processQuery.Update(processId, process);
        }

        public void Cancel(string processId)
        {
            //if it's already cancelled but another thread just finished right after that
            if (GlobalStore.Processes.Get(processId) == null) return;

            var tokenSource = GlobalStore.Processes.Get(processId).CancellationToken;
            tokenSource.Cancel();
            var process = GlobalStore.Processes.Get(processId).Process;
            process.Status = (int)ProcessStatusEnum.Cancelling;
            processQuery.Update(processId, process);
        }

        public void Cancelled(string processId)
        {
            var process = GlobalStore.Processes.Get(processId).Process;
            process.End = DateTime.UtcNow;
            process.Status = (int)ProcessStatusEnum.Cancelled;

            GlobalStore.Processes.Remove(processId);

            processQuery.Update(processId, process);
        }

        public void AddError(string processId, string message)
        {
            var process = GlobalStore.Processes.Get(processId).Process;
            process.ErrorMessages.Add(message);
            processQuery.Update(processId, process);
        }

        public void Interrupted(string processId, Exception ex)
        {
            Serilog.Log.Error(ex, ProcessResources.FatalErrorOccuredDuringTheOperation + " {ProcessID}", processId);

            var process = GlobalStore.Processes.IsExist(processId) ? GlobalStore.Processes.Get(processId).Process : processQuery.Get(GlobalStore.InstanceId, processId);
            process.End = DateTime.UtcNow;

            process.ErrorMessages.Add(ProcessResources.FatalErrorOccuredDuringTheOperation);
            if (ex is Common.Exceptions.SlambyException) process.ErrorMessages.Add(ex.Message);
            process.Status = (int)ProcessStatusEnum.Error;

            if (GlobalStore.Processes.IsExist(processId))
            {
                GlobalStore.Processes.Remove(processId);
            }
            processQuery.Update(processId, process);
        }
    }
}