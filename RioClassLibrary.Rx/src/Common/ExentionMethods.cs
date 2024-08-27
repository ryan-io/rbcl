using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace rbcl.rx {
	public static class ExentionMethods {
		public static IObservable<IList<T>> Quiescent<T> (this IObservable<T> src, TimeSpan delay, IScheduler scheduler) {
			IObservable<int> onoffs =
				src.SelectMany(_ => {
					var returnObservable = Observable.Return(1, scheduler);
					var concatObservable = Observable.Return(-1, scheduler).Delay(delay, scheduler);
					return returnObservable.Concat(concatObservable);
				}, (_, delta) => delta);

			IObservable<int> outstanding = onoffs.Scan(0, (total, delta) => total + delta); // yuck...
			IObservable<int> zeroCrossings = outstanding.Where(total => total == 0);
			return src.Buffer(zeroCrossings);
		}
	}
}
