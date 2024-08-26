using System.Reactive.Linq;

namespace rbcl.console;

internal class RxExample {
	public void Run () {

		//var observable = SomeNumbers();
		//observable.Subscribe(x => { Console.WriteLine($"Number {x}"); },
		//	onCompleted: () => { Console.WriteLine("Complete!"); });
	}

	public void TimerTest () {
		var timer = Observable.Interval(TimeSpan.FromSeconds(2));
		timer.Subscribe(elapsedTime => Console.WriteLine($"Elapsed time is {elapsedTime} sec."));
	}

	public IObservable<int> GenerateTest (int start, int count) {
		int max = start + count;
		return Observable.Generate(
			start,
			value => value < max,
			value => value + 1,
			value => value);
	}

}