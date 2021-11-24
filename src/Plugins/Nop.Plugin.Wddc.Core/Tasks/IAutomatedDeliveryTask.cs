using Nop.Services.Tasks;

namespace Nop.Plugin.Wddc.Core.Tasks
{
    public interface IAutomatedDeliveryTask : IScheduleTask
    {
        string Name { get; }
        bool Enabled { get; }
    }
}