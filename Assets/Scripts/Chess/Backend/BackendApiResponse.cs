using System;
using System.Collections.Generic;

[Serializable]
public class BackendApiResponse<T>
{
    public int code;
    public string message;
    public T result;
    public Dictionary<string, string> errors;
    public string path;
    public string timestamp;
}
