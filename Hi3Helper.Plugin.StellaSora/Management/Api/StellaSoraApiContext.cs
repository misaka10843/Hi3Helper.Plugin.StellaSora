using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StellaSoraBaseResponse<StellaSoraGameConfigData>))]
[JsonSerializable(typeof(StellaSoraBaseResponse<StellaSoraCdnData>))]
[JsonSerializable(typeof(StellaSoraBaseResponse<StellaSoraBaseConfigData>))]
[JsonSerializable(typeof(StellaSoraBaseResponse<StellaSoraResourceData>))]
[JsonSerializable(typeof(StellaSoraBaseResponse<StellaSoraConfigJsonData>))]
[JsonSerializable(typeof(StellaSoraManifest))]
[JsonSerializable(typeof(StellaSoraAuthHead))]
[JsonSerializable(typeof(StellaSoraAuthHeader))]
public partial class StellaSoraApiContext : JsonSerializerContext
{
}