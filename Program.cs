using System.Threading.Tasks;

namespace Singer
{
	// almost everything i changed was cuz rider prompted me to, maybe you should do that too
	public class Program
	{
		public static async Task Main(string[] args) // you can make this static
		{
			var bot = new Singer();
			await bot.RunAsync();
			await Task.Delay(-1);
		}
	}
}