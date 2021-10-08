using Nop.Services.Tasks;

namespace Nop.Plugin.Wddc.Core.Tasks
{
    public interface IAutomatedDeliveryTask : ITask
    {
        string Name { get; }
        bool Enabled { get; }
    }
}