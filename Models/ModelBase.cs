
using System.Threading;
using System.Threading.Tasks;
using static HandyTools.Types;

namespace HandyTools.Models
{
    public interface ModelBase
    {
        TypeAIAPI APIType { get; }
        Task<string> CompletionAsync(string userInput, float temperature, CancellationToken cancellationToken = default);
    }
}
