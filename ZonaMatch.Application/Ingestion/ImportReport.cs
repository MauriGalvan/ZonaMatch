namespace ZonaMatch.Application.Ingestion
{
    public sealed class SourceImportResult
    {
        public SourceImportResult(string dataSourceCode, string target)
        {
            DataSourceCode = dataSourceCode;
            Target = target;
        }

        public string DataSourceCode { get; }
        public string Target { get; }
        public int Read { get; set; }
        public int Imported { get; set; }
        public int Invalid { get; set; }
        // unidades sin padre resoluble o POIs sin regla de categoría
        public int Skipped { get; set; }
    }

    public sealed record ImportReport(IReadOnlyList<SourceImportResult> Sources);

    public sealed record ZoneBuildReport(int Zones, int Adjacencies);

    public sealed record DeduplicationReport(int Candidates, int Duplicates);
}
