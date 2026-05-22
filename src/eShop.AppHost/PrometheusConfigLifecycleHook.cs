using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace eShop.AppHost;

/// <summary>
/// Aspire lifecycle hook: Prometheus container başlamadan önce, endpoint bilgilerinden
/// prometheus.yml dosyasını dinamik olarak oluşturur.
/// </summary>
internal sealed class PrometheusConfigLifecycleHook : IDistributedApplicationEventingSubscriber
{
    private readonly string _prometheusConfigDir;
    private readonly EndpointReference[] _endpoints;

    public PrometheusConfigLifecycleHook(string prometheusConfigDir, EndpointReference[] endpoints)
    {
        _prometheusConfigDir = prometheusConfigDir;
        _endpoints = endpoints;
    }

    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken = default)
    {
        // BeforeStartEvent: TargetPort (container-internal port) bu aşamada erişilebilir,
        // host'a atanan Port ise allocation sonrasında gelir — biz zaten container-adı:targetPort kullanıyoruz.
        eventing.Subscribe<BeforeStartEvent>((_, ct) =>
        {
            WritePrometheusConfig();
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    private void WritePrometheusConfig()
    {
        var scrapeConfigs = new System.Text.StringBuilder();
        scrapeConfigs.AppendLine("global:");
        scrapeConfigs.AppendLine("  scrape_interval: 15s");
        scrapeConfigs.AppendLine("  evaluation_interval: 15s");
        scrapeConfigs.AppendLine();
        scrapeConfigs.AppendLine("scrape_configs:");

        var seen = new HashSet<string>();
        foreach (var endpoint in _endpoints)
        {
            var name = endpoint.Resource.Name;
            if (!seen.Add(name)) continue;

            bool isRabbitMq = name == "eventbus";
            // TargetPort: container'ın kendi dinlediği iç port (host port değil).
            // Docker network içinde servisler container-adı:targetPort ile birbirine ulaşır.
            int port = isRabbitMq ? 15692 : (endpoint.TargetPort ?? 8080);
            scrapeConfigs.AppendLine();
            scrapeConfigs.AppendLine($"  - job_name: '{name}'");
            scrapeConfigs.AppendLine($"    static_configs:");
            scrapeConfigs.AppendLine($"      - targets: ['{name}:{port}']");
            scrapeConfigs.AppendLine($"    metrics_path: /metrics");
        }

        Directory.CreateDirectory(_prometheusConfigDir);
        File.WriteAllText(
            Path.Combine(_prometheusConfigDir, "prometheus.yml"),
            scrapeConfigs.ToString());
    }
}
