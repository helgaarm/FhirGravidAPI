using System.Security.Cryptography;
using System.Text;

namespace PopulationDataFacade.Core;

public static class FetalPatientId
{
    public static string Create(string maternalPatientId, string pregnancyContextId, int sourceFetusId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(maternalPatientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pregnancyContextId);
        if (sourceFetusId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceFetusId));

        var input = Encoding.UTF8.GetBytes($"{maternalPatientId}\0{pregnancyContextId}\0{sourceFetusId}");
        var digest = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        return $"fetus-{digest[..40]}";
    }
}
