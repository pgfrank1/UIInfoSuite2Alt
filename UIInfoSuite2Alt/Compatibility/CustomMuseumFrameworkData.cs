using System.Collections.Generic;

namespace UIInfoSuite2Alt.Compatibility;

internal enum CmfMatchType
{
  Any,
  All,
}

internal class CmfMuseumData
{
  public string Id { get; set; } = "";
  public CmfOwnerData? Owner { get; set; }
  public List<CmfDonationRequirement> DonationRequirements { get; set; } = [];
  public List<CmfDonationRequirement> BlacklistedDonations { get; set; } = [];
  public List<CmfDonationRequirement> WhitelistedDonations { get; set; } = [];
}

internal class CmfOwnerData
{
  public string? Name { get; set; }
}

internal class CmfDonationRequirement
{
  public string? Id { get; set; }
  public List<int>? Categories { get; set; }
  public List<string>? ContextTags { get; set; }
  public List<string>? ItemIds { get; set; }
  public CmfMatchType MatchType { get; set; } = CmfMatchType.Any;
}
