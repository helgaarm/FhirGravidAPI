using System.Security.Cryptography;
using System.Text;

namespace PopulationDataFacade.Core;

public static class FetalPatientId
{
    public static string Create(string maternalPatientId, int sourceFetusId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(maternalPatientId);
        if (sourceFetusId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceFetusId));

        var input = Encoding.UTF8.GetBytes($"{maternalPatientId}:{sourceFetusId}");
        var digest = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        return $"fetus-{digest[..40]}";
    }
}
