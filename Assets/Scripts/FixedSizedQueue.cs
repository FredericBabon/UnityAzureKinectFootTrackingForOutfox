using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class FixedSizedQueue
{
    readonly ConcurrentQueue<float> queue = new ConcurrentQueue<float>();

    public int Capacity { get; private set; }

    public FixedSizedQueue(int capacity)
    {
        Capacity = capacity;
    }

    public int GetSize()
    {
        return queue.Count;
    }

    public void Enqueue(float obj)
    {
        queue.Enqueue(obj);

        while (GetSize() > Capacity)
        {
            float outObj;
            queue.TryDequeue(out outObj);
        }
    }

    public float GetMean()
    {
        float sum = 0;
        if (GetSize() == 0)
            return sum;

        foreach (float val in queue)
        {
            sum += val;
        }
        sum /= GetSize();
        return sum;
    }

    public void Clear()
    {
        queue.Clear();
    }
}