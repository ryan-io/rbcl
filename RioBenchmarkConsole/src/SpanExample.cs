namespace rbcl.console;

internal class SpanExample {
	public void Run () {
		var array = new int[103];
		array[0] = 100;

		rbcl.Span<int> mySpan = new rbcl.Span<int>(ref array);
		rbcl.Span<int> mySpan2 = new rbcl.Span<int>(mySpan);
		Console.WriteLine(mySpan[0]);

		Console.WriteLine(mySpan2[0]);
		mySpan2[0] = 1212;
		Console.WriteLine(mySpan2[0]);

		unsafe {
			int* spans = stackalloc int[1000];
			spans[0] = 1006;
			//ref var spanPtr = ref spans;	can explicitly create the reference to this span
			rbcl.Span<int> mySpan3 = new rbcl.Span<int>(spans, 1000);
			Console.WriteLine(mySpan3[0]);
		}

		Console.WriteLine($"mySpan length: {mySpan.Length}");

		var mySpanSlice = mySpan.Slice(19);
		mySpanSlice[0] = 120;

		Console.WriteLine(mySpanSlice[0]);
		Console.WriteLine(mySpanSlice.Length);

	}
}