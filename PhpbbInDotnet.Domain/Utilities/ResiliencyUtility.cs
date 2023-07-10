using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class ResiliencyUtility
    {
        /// <summary>
        /// If <paramref name="evaluateSuccess"/> returns false, then it retries the <paramref name="toDo"/> logic once, after applying the <paramref name="fix"/> logic.
        /// </summary>
        /// <param name="toDo">Logic to retry if failing.</param>
        /// <param name="evaluateSuccess">Logic to evaluate the success of the initial run.</param>
        /// <param name="fix">Logic to run if the initial run has failed, before retrying it.</param>
        /// <returns></returns>
        public static async Task RetryOnceAsync(Func<Task> toDo, Func<bool> evaluateSuccess, Action fix)
        {
            await toDo();
            if (!evaluateSuccess())
            {
                fix();
                await toDo();
            }
        }

		/// <summary>
		/// If <paramref name="evaluateSuccess"/> returns false, then it retries the <paramref name="toDo"/> logic once, after applying the <paramref name="fix"/> logic.
		/// </summary>
		/// <param name="toDo">Logic to retry if failing.</param>
		/// <param name="evaluateSuccess">Logic to evaluate the success of the initial run.</param>
		/// <param name="fix">Logic to run if the initial run has failed, before retrying it.</param>
		/// <returns></returns>
		public static void RetryOnce(Action toDo, Func<bool> evaluateSuccess, Action fix)
		{
			toDo();
			if (!evaluateSuccess())
			{
				fix();
				toDo();
			}
		}
	}
}
