//var ex = new NaiveAsyncExample();
//ex.RunQueueWorkerThreadNonDeterministic();

using rbcl.console;

// will be disposed once 'main' goes out of scope regardless
using var ex = new AsyncTaskThreadExample();
ex.RunQueueWorkerThreadNonDeterministic();


Console.ReadLine();