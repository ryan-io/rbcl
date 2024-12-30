namespace rbcl.console;

internal class SpanExample {
	public void Run () {
		var array = new int[103];
		array[0] = 100;

		naive.NaiveSpan<int> myNaiveSpan = new naive.NaiveSpan<int>(ref array);
		naive.NaiveSpan<int> mySpan2 = new naive.NaiveSpan<int>(myNaiveSpan);
		Console.WriteLine(myNaiveSpan[0]);

		Console.WriteLine(mySpan2[0]);
		mySpan2[0] = 1212;
		Console.WriteLine(mySpan2[0]);

		unsafe {
			int* spans = stackalloc int[1000];
			spans[0] = 1006;
			//ref var spanPtr = ref spans;	can explicitly create the reference to this naiveSpan
			naive.NaiveSpan<int> mySpan3 = new naive.NaiveSpan<int>(spans, 1000);
			Console.WriteLine(mySpan3[0]);
		}

		Console.WriteLine($"myNaiveSpan length: {myNaiveSpan.Length}");

		var mySpanSlice = myNaiveSpan.Slice(19);
		mySpanSlice[0] = 120;

		Console.WriteLine(mySpanSlice[0]);
		Console.WriteLine(mySpanSlice.Length);

	}
}