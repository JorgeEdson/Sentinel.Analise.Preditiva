using Elastic.Clients.Elasticsearch;
using Sentinel.Analise.Preditiva.Modelos;

namespace Sentinel.Analise.Preditiva.Services
{
    public class ElasticsearchService(IConfiguration configuration, ElasticsearchClient elasticsearchClient)
    {
        private readonly ElasticsearchClient _client = elasticsearchClient;
        private readonly string _indexName = configuration["Elasticsearch:Index"] ?? "analise_preditiva"; 

        public async Task IndexarDadosAsync(AnalisePreditiva analisePreditiva)
        {
            var response = await _client.IndexAsync(analisePreditiva, idx => idx.Index(_indexName));

            if (!response.IsValidResponse)
            {
                throw new Exception($"Erro ao indexar no Elasticsearch: {response.DebugInformation}");
            }
        }
    }
}
