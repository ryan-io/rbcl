using rbcl.console;

var ex = new RxExample();
var obs = ex.GenerateTest(10, 15);
var observer = obs.Subscribe(x => { Console.WriteLine($"Number {x}"); },
	onCompleted: () => { Console.WriteLine("Complete!"); });

observer.Dispose();

Console.ReadLine();