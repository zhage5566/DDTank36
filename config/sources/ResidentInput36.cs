using System.IO;
using System.Threading;
// Native console loops treat EOF as another command and can flood logs when
// launched hidden. Server threads continue while the unused command input waits.
internal sealed class ResidentInput36:TextReader {
 public override string ReadLine(){Thread.Sleep(Timeout.Infinite);return null;}
}
