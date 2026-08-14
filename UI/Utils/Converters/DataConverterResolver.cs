using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Converters;


/// <summary>Never returns null: categories with no registered <see cref="IDataConverter"/> resolve to a
/// fallback converter instead, so callers don't need to null-check.</summary>
public delegate IDataConverter DataConverterResolver(DataCategory category);
