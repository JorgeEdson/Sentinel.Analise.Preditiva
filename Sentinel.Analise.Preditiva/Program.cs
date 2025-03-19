using Elastic.Clients.Elasticsearch;
using RabbitMQ.Client;
using Sentinel.Analise.Preditiva.Services;

var builder = WebApplication.CreateBuilder(args);

//rabbitMQ
var rabbitMQHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitMQUser = builder.Configuration["RabbitMQ:User"] ?? "admin";
var rabbitMQPassword = builder.Configuration["RabbitMQ:Password"] ?? "admin";
var factory = new ConnectionFactory() { HostName = rabbitMQHost, UserName = rabbitMQUser, Password = rabbitMQPassword };
var connection = factory.CreateConnection();
builder.Services.AddSingleton<IConnection>(connection);
builder.Services.AddSingleton<RabbitMQService>();

//elasticsearch
var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"]
    ?? throw new ArgumentNullException(nameof(builder.Configuration), "A URL do Elasticsearch não foi configurada corretamente.");
var elasticsearchClientSettings = new ElasticsearchClientSettings(new Uri(elasticsearchUri));
var elasticsearchClient = new ElasticsearchClient(elasticsearchClientSettings);
builder.Services.AddSingleton(elasticsearchClient);
builder.Services.AddSingleton<ElasticsearchService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.Run();