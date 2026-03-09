using PSOpenAD;

internal interface IDebugCommand
{
    IEnumerable<OpenADEntity> Run();
}