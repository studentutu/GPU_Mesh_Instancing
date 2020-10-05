//----------------------------------------------
// Mesh Animator
// Flick Shot Games
// http://www.flickshotgames.com
//----------------------------------------------

using System.Collections.Generic;

namespace FSG.MeshAnimator
{
    /// <summary>
    /// Static storage for reusable arrays
    /// </summary>
	public static class AllocatedArray<T>
	{
		private static T defaultValue = default(T);
        private static List<T[]> allocatedArrays = new List<T[]>(100);

        /// Allocates a new array        
		private static T[] AllocateArray(int size)
		{
			return new T[size];
		}

        /// Returns an array of T of the specified size from the pool or create a new one        
		public static T[] Get(int size)
		{
            lock (allocatedArrays)
            {
                for (int i = 0; i < allocatedArrays.Count; i++)
                {
                    T[] array = allocatedArrays[i];
                    if (array.Length == size)
                    {
                        allocatedArrays.RemoveAt(i);
                        return array;
                    }
                }
                return AllocateArray(size);
            }
		}
        
        /// Return a previously allocated array to the array pool
        public static void Return(T[] array, bool resetValues = true)
        {
            if (resetValues)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = defaultValue;
                }
            }
            lock (allocatedArrays)
            {
                allocatedArrays.Add(array);
            }
        }
	}
}