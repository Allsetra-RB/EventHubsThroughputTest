using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public interface IRunnable
	{
		Task Start();
		Task Stop();
	}
}
