using UnityEngine;

public enum ShaderReference
{
    Lit,
    Cynty_Generic_Basic
}

public static class YJ_LightData
{
    public static string GetEmissionColorName(ShaderReference shaderReference)
    {
        switch (shaderReference)
        {
            case ShaderReference.Lit:
                return "_EmissionColor";

            case ShaderReference.Cynty_Generic_Basic:
                return "_Emission_Color";

            default:
                return "_EmissionColor";
        }
    }

    public static int GetEmissionColorId(ShaderReference shaderReference)
    {
        return Shader.PropertyToID(GetEmissionColorName(shaderReference));
    }
}
