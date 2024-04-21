using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class FixedSizedQueueVector3
{
    readonly ConcurrentQueue<Vector3> queue = new ConcurrentQueue<Vector3>();

    public int Capacity { get; private set; }

    public FixedSizedQueueVector3(int capacity)
    {
        Capacity = capacity;
    }

    public int GetSize()
    {
        return queue.Count;
    }

    public void Enqueue(Vector3 obj)
    {
        queue.Enqueue(obj);

        while (GetSize() > Capacity)
        {
            Vector3 outObj;
            queue.TryDequeue(out outObj);
        }
    }

    public Vector3 GetMean()
    {
        Vector3 sum = Vector3.zero;
        if (GetSize() == 0)
            return sum;

        foreach (Vector3 val in queue)
        {
            sum += val;
        }
        sum /= (float)GetSize();
        return sum;
    }

    public void Clear()
    {
        queue.Clear();
    }
}