using System;
using System.Collections.Generic;

namespace StormRouterVisualization.Services
{
    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _elements = new List<(TElement, TPriority)>();

        public int Count => _elements.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _elements.Add((element, priority));
            _elements.Sort((x, y) => x.Priority.CompareTo(y.Priority));
        }

        public TElement Dequeue()
        {
            if (_elements.Count == 0)
                throw new InvalidOperationException("Queue is empty");
            
            var item = _elements[0];
            _elements.RemoveAt(0);
            return item.Element;
        }
    }
}