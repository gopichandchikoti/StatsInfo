// Synthetic CSV producer for adapter lifecycle tests only. Never shipped with StatsInfo.
using System;
using System.Linq;
using System.Threading;

class PresentMonFixture
{
    static void Main(string[] args)
    {
        if (args.Contains("--terminate_existing_session")) return;
        if (args.Contains("--wait")) { Thread.Sleep(30000); return; }
        int index = Array.IndexOf(args, "--process_id");
        string pid = index >= 0 ? args[index + 1] : "0";
        Console.WriteLine(new string('x', 17000));
        Console.WriteLine("Application,ProcessID,SwapChainAddress,MsBetweenPresents,TimeInSeconds");
        Console.WriteLine("SyntheticTestOnly,0,0x1,10,0");
        Console.WriteLine("SyntheticTestOnly," + pid + ",0x1,NaN,0");
        for (int i = 0; i < 1100; i++) Console.WriteLine("SyntheticTestOnly," + pid + ",0x1,10," +
            (i / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine("SyntheticTestOnly," + pid + ",0x2,100,12");
        Console.Out.Flush();
        Thread.Sleep(5000);
    }
}
