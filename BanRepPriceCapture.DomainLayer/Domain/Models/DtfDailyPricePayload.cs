namespace BanRepPriceCapture.DomainLayer.Domain.Models;

public sealed record DtfDailyPricePayload(
    int CodAtivo,
    string Data,
    string CodPraca,
    int CodFeeder,
    int CodCampo,
    decimal Preco,
    decimal FatorAjuste,
    bool Previsao,
    bool IsRebook);
