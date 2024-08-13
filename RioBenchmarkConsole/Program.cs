//var ex = new NaiveAsyncExample();
//ex.RunQueueWorkerThreadNonDeterministic();

using Newtonsoft.Json;
using RioBenchmarkConsole;

var mqttObj = new MqttJsonDemo() { Id = 1, Name = "Test", Payload = "12, 323, 232" };
var json = JsonConvert.SerializeObject(mqttObj);
var conversion = JsonConvert.DeserializeObject<MqttJsonDemo>(json);

Console.WriteLine(json);
Console.WriteLine(conversion?.Name);
Console.ReadLine();