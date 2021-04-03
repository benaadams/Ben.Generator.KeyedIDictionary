
using Http.Headers;

public class Usage
{
    public string DoSomething(IHttpHeaderDictionary dictionary)
    {
        return dictionary.AcceptEncoding;
    }
}

