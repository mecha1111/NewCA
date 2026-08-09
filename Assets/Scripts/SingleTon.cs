using System;

using UnityEngine;

/// <summary>
/// General thread safe singleton class
/// </summary>
/// <typeparam name="_T"></typeparam>
public abstract class SingleTon<_T> : IDisposable where _T : class, new()
{
    private static _T m_instance = null;
    private static readonly object m_lock = new object();


    ~SingleTon()
    {
        Dispose(false);
    }

    /// <summary>
    /// Get the instance of the singleton class
    /// </summary>
    public static _T Instance
    {
        get
        {
            lock (m_lock)
            {
                if (m_instance == null)
                {
                    m_instance = new _T();
                }
                return m_instance;
            }
        }
    }

    /// <summary>
    /// Dispose the instance of the singleton 
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    protected abstract void Dispose(bool bisDisposing);
}

public abstract class SingleTonForGameObject<_T> : MonoBehaviour, IDisposable where _T : class
{
    private static _T m_instance = null;
    private static readonly object m_lock = new object();


    public static _T Instance
    {
        get
        {
            lock (m_lock)
            {
                if (m_instance == null)
                {
                    // 예외 대신 null 반환하여 성능 저하 방지
                    // SetInstance가 호출되지 않은 경우를 처리
                    return null;
                }
                return m_instance;
            }
        }
    }

    public static void SetInstance(in _T instance)
    {
        lock (m_lock)
        {
            //if (m_instance == null)
            //{
            m_instance = instance;
            //DontDestroyOnLoad((m_instance as MonoBehaviour).gameObject);
            //}
        }
    }

    public bool BIsInstanceNull
    {
        get
        {
            return m_instance == null;
        }
    }

    public void Dispose()
    {
        m_instance = null;

        Dispose(true);
    }

    protected abstract void Dispose(bool bisDisposing);
}