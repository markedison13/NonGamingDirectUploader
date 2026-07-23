namespace NonGamingDirectUploader.Models
{
    /// <summary>
    /// The expected data type of a column, used by the automation import to
    /// validate each row's values by content rather than by matching header
    /// text — since files dropped by another app may use different headers
    /// (or none at all), but the column order/format is still expected to
    /// match the target table.
    /// </summary>
    public enum ColumnDataType
    {
        Text,
        Number,
        Date
    }
}