using System.Threading.Tasks;

public class TDInitializeModel
{
    private static TDInitializeModel m_api;
    public static TDInitializeModel api
    {
        get
        {
            return m_api ??= new TDInitializeModel();
        }
    }
    
    public readonly TaskCompletionSource<bool> createGridCompletion = new TaskCompletionSource<bool>();
}
