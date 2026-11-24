# Cloud Streams Grafana dashboards

This folder contains the official Grafana dashboards for monitoring Cloud Streams deployments.

| File | Description |
|------|-------------|
| [`cloud-streams-overview.json`](./cloud-streams-overview.json) | Gateway ingestion (outcomes, refusal reasons, latency), broker delivery (per subscription, latency, deferred acknowledgements, lag), inbound HTTP (gateway + broker) and outbound HTTP (broker → subscribers) health. |

The dashboard targets a **Prometheus** data source fed with the OpenTelemetry metrics that the Cloud Streams gateway and broker export over OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`), either:

- directly, through Prometheus' native OTLP receiver (`--web.enable-otlp-receiver`, used by the `grafana/otel-lgtm` image in [`deployments/docker-compose`](../docker-compose)), or
- through an OpenTelemetry Collector, using its `prometheusremotewrite` exporter, or its `prometheus` exporter scraped by Prometheus.

It requires Grafana 10.4 or later (`schemaVersion` 39).

## Importing the dashboard

### Grafana UI

1. Open **Dashboards → New → Import**.
2. Upload `cloud-streams-overview.json` (or paste its content) and click **Load**.
3. Pick a folder and click **Import**.
4. Select your Prometheus data source in the **Data source** variable at the top of the dashboard.

### Provisioning

Copy the JSON file next to Grafana (for example `/var/lib/grafana/dashboards/cloud-streams/`) and declare a dashboard provider, e.g. `provisioning/dashboards/cloud-streams.yaml`:

```yaml
apiVersion: 1

providers:
  - name: cloud-streams
    folder: Cloud Streams
    type: file
    disableDeletion: false
    allowUiUpdates: true
    updateIntervalSeconds: 60
    options:
      path: /var/lib/grafana/dashboards/cloud-streams
      foldersFromFilesStructure: false
```

With the docker-compose stack of this repository, you can mount it alongside the existing provisioning folder:

```yaml
  otel-lgtm:
    image: grafana/otel-lgtm
    volumes:
      - ./config/prometheus/prometheus.yml:/otel-lgtm/prometheus.yaml
      - ./config/grafana/provisioning:/otel-lgtm/grafana/conf/provisioning
      - ../grafana/cloud-streams-overview.json:/var/lib/grafana/dashboards/cloud-streams/cloud-streams-overview.json:ro
```

On Kubernetes with the Grafana Helm chart or the Grafana sidecar, put the JSON in a `ConfigMap` labelled `grafana_dashboard: "1"`:

```shell
kubectl create configmap cloud-streams-dashboards \
  --from-file=cloud-streams-overview.json=deployments/grafana/cloud-streams-overview.json
kubectl label configmap cloud-streams-dashboards grafana_dashboard=1
```

## Dashboard variables

| Variable | Type | Description |
|----------|------|-------------|
| `datasource` | Data source (Prometheus) | The Prometheus data source that holds the Cloud Streams metrics. |
| `job_label` | Custom (`job`, `exported_job`, `service_name`) | The Prometheus label that carries the OpenTelemetry `service.name` resource attribute. Use `job` (default) with Prometheus' OTLP receiver or the Collector's `prometheusremotewrite` exporter, `exported_job` when Prometheus scrapes the Collector's `prometheus` exporter (the original `job` is renamed), or `service_name` when that resource attribute is promoted to a label. |
| `gateway` | Query: `label_values(http_server_request_duration_seconds_count, ${job_label})`, regex `/.*gateway.*/` | The gateway service(s) to display. |
| `broker` | Query: `label_values(http_server_request_duration_seconds_count, ${job_label})`, regex `/.*broker.*/` | The broker service(s) to display. |
| `subscription` | Query on the `subscription` label of the broker metrics | The subscription(s) to display in the broker panels, by qualified name. |
| `stream` | Query on the `stream` label of the broker metrics | The source stream(s) to display in the broker panels (`All` for non-partitioned subscriptions). |
| `server_address` | Query on the `server_address` label of `http_client_request_duration_seconds_count` | The outbound destination host(s) to display in the broker dispatch panels. |

The service names default to `cloud-streams-gateway` and `cloud-streams-broker`, and can be overridden with the `CLOUDSTREAMS_GATEWAY_NAME` and `CLOUDSTREAMS_BROKER_NAME` environment variables (for example `gateway-mozart`, `broker-content`). The `gateway` and `broker` variables match any service whose name contains `gateway` / `broker`; if your names don't, edit the variables' **Regex** (and **Custom all value**) in the dashboard settings.

## Metrics used by the dashboard

### OTLP to Prometheus name translation

Cloud Streams emits metrics with the .NET `System.Diagnostics.Metrics` API and exports them over OTLP. When they land in Prometheus, OpenTelemetry metric names and attributes are translated:

- characters that are not valid in Prometheus names (`.`, `-`, spaces, ...) become `_`: `http.server.request.duration` → `http_server_request_duration`, `http.response.status_code` → `http_response_status_code`;
- the unit is appended as a suffix (`s` → `_seconds`);
- monotonic counters get a `_total` suffix;
- histograms are exposed as `_bucket`, `_count` and `_sum` series.

The custom Cloud Streams counters and gauges declare UCUM annotations as units (`{event}`, `{failure}`, `{timeout}`), which are not appended to their name, while their histograms declare seconds (`s`), which are: `cloud_streams_broker_delivery_duration` becomes `cloud_streams_broker_delivery_duration_seconds_bucket`. Depending on the Prometheus / Collector version and its translation settings, counters may or may not carry a `_total` suffix (for example `cloud_streams_gateway_events_ingested_count_total`), so the dashboard selects them by name prefix, e.g. `{__name__=~"cloud_streams_gateway_events_ingested_count.*"}`. Earlier versions of Cloud Streams declared non-UCUM units (`Cloud Event`, `Failure`), which some translators appended to the name (`..._count_Cloud_Event_total`): the prefix selection matches both. Run `count by (__name__) ({__name__=~"cloud_streams_.*"})` in Grafana Explore to see the exact names in your environment.

### Custom Cloud Streams metrics

Every label has a bounded cardinality, so that the metrics can safely be stored by Prometheus whatever publishers and subscribers do.

| Instrument (OpenTelemetry) | Type | Unit | Attributes | Emitted by | Meaning |
|----------------------------|------|------|------------|------------|---------|
| `cloud_streams_gateway_events_ingested_count` | Counter | `{event}` | `source` | Gateway | Cloud events admitted and appended to the event store. |
| `cloud_streams_gateway_events_ignored_count` | Counter | `{event}` | `source` | Gateway | Cloud events ignored by the split-horizon policy (`loopAction: ignore`). |
| `cloud_streams_gateway_events_rejected_count` | Counter | `{event}` | `reason` | Gateway | Cloud events refused by the gateway's split-horizon (`split_horizon`) or authorization (`authorization`) policies. |
| `cloud_streams_gateway_events_invalid_count` | Counter | `{event}` | `reason` | Gateway | Cloud events refused because their context attributes (`attributes`) or their data (`schema`) are invalid. |
| `cloud_streams_gateway_append_failures_count` | Counter | `{failure}` | `source` | Gateway | Admitted cloud events that could not be appended to the event store. |
| `cloud_streams_gateway_ingestion_duration` | Histogram | `s` | `outcome` | Gateway | Duration of the ingestion of cloud events, from their admission to their storage. `outcome` is `stored`, `ignored`, `rejected`, `invalid` or `failed` (gateway not ready, or event store failure). |
| `cloud_streams_broker_events_published_count` | Counter | `{event}` | `subscription`, `stream` | Broker | Cloud events successfully dispatched to a subscriber. |
| `cloud_streams_broker_delivery_failures_count` | Counter | `{failure}` | `subscription`, `stream` | Broker | Failed delivery attempts (non-success status code, transport error or timeout), each retry included; each one moves the subscription's subscriber to the `unreachable` state and triggers the retry policy. |
| `cloud_streams_broker_delivery_duration` | Histogram | `s` | `subscription`, `stream`, `outcome` | Broker | Duration of delivery attempts, from the request to the subscriber's response, mutation included. `outcome` is `delivered`, `deferred` (`202 Accepted`) or `failed`. |
| `cloud_streams_broker_pending_acks` | UpDownCounter | `{event}` | `subscription`, `stream` | Broker | Events whose acknowledgement the subscriber has deferred (`202 Accepted`) and not yet resolved. At most one per subscription. |
| `cloud_streams_broker_ack_duration` | Histogram | `s` | `subscription`, `stream`, `outcome` | Broker | How long deferred acknowledgements have been pending before being resolved. `outcome` is `ack`, `nack` or `expired`. Acknowledgements that were pending when the broker restarted are not recorded. |
| `cloud_streams_broker_ack_timeouts_count` | Counter | `{timeout}` | `subscription`, `stream` | Broker | Deferred acknowledgements that expired before the subscriber called back. |
| `cloud_streams_broker_subscription_lag` | Gauge | `{event}` | `subscription`, `stream` | Broker | Records of its stream a subscription has yet to handle. See [Subscription lag](#subscription-lag). |

- `subscription` is the qualified name of the subscription (its name, qualified by its namespace if it has one), and `stream` the name of the stream it reads from: the stream of its partition, or `All` when it is not partitioned. Broker metrics used to be labelled with the subscriber URI instead, which has an unbounded cardinality and may carry credentials in its query string.
- `source` is the source of the cloud event, without its query string. Each gateway labels at most 100 distinct sources, in the order it first admits them: past them, sources are labelled `other`. Refused events are not labelled with their source, which is chosen by whoever sends them, admitted or not.
- The type of cloud events is not used as a label: its cardinality is unbounded. Use traces, whose spans carry the `event.type` attribute, to break ingestion and delivery down by type.

### ASP.NET Core and HttpClient metrics

Both services enable the ASP.NET Core and HttpClient OpenTelemetry instrumentations, which expose the standard [HTTP semantic convention metrics](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/):

| Prometheus series | Source instrument | Labels used |
|-------------------|-------------------|-------------|
| `http_server_request_duration_seconds_bucket` / `_count` | `http.server.request.duration` (s, histogram) | `http_request_method`, `http_route`, `http_response_status_code` |
| `http_client_request_duration_seconds_bucket` / `_count` | `http.client.request.duration` (s, histogram) | `server_address`, `http_response_status_code`, `error_type` |

The gateway also hosts the Cloud Streams resources API, so its inbound HTTP panels include API traffic; the **Publish endpoint** panel isolates the cloud event ingestion route (`http_route="api/events/pub"`). The broker's outbound HTTP panels mostly reflect subscriber dispatch, but may also include other outbound HTTP traffic of the broker (for example gRPC calls to the event store): use the **Destination** variable to narrow them down.

## Subscription lag

Each broker reports the lag of the subscriptions it handles with the `cloud_streams_broker_subscription_lag` gauge: the amount of records of its stream a subscription has yet to handle, which is to say acknowledge, filter out or drop. Only the broker replica that holds a subscription's lease handles it, so a subscription's lag is reported once. It is shown by the **Lag by subscription** panel, and can be alerted on, for example:

```promql
max by (subscription) (cloud_streams_broker_subscription_lag) > 1000
```

The gauge counts records up to the last one that passed the subscription's filters, as observed by the broker: records filtered out at the end of the stream are not counted, since the subscription has nothing to deliver. A faulted subscription keeps reporting a growing lag until it is resumed.

The lag is also available from the `GET /api/resources/v1/subscriptions/health` endpoint of the Cloud Streams API (hosted by the gateway), which computes it from the length of each subscription's stream and its acknowledged offset, and returns a root-level JSON array with one entry per subscription:

```json
[
  {
    "name": "my-subscription",
    "namespace": "default",
    "phase": "active",
    "partitionId": "my-stream",
    "ackedOffset": 1200,
    "partitionLength": 1250,
    "lag": 50,
    "subscriberState": "reachable",
    "subscriberReason": null
  }
]
```

It can be displayed in Grafana with the [Infinity](https://grafana.com/grafana/plugins/yesoreyeram-infinity-datasource/) (or JSON API) data source. Create an Infinity data source allowing the Cloud Streams API URL, then add a panel with a target such as:

```json
{
  "datasource": { "type": "yesoreyeram-infinity-datasource", "uid": "<infinity-datasource-uid>" },
  "type": "table",
  "title": "Subscription lag",
  "targets": [
    {
      "refId": "A",
      "type": "json",
      "source": "url",
      "format": "table",
      "parser": "backend",
      "url": "http://cloud-streams-gateway/api/resources/v1/subscriptions/health",
      "url_options": { "method": "GET" },
      "root_selector": "",
      "columns": [
        { "selector": "name", "text": "Name", "type": "string" },
        { "selector": "namespace", "text": "Namespace", "type": "string" },
        { "selector": "phase", "text": "Phase", "type": "string" },
        { "selector": "partitionId", "text": "Partition", "type": "string" },
        { "selector": "ackedOffset", "text": "Acked offset", "type": "number" },
        { "selector": "partitionLength", "text": "Partition length", "type": "number" },
        { "selector": "lag", "text": "Lag", "type": "number" },
        { "selector": "subscriberState", "text": "Subscriber state", "type": "string" },
        { "selector": "subscriberReason", "text": "Subscriber reason", "type": "string" }
      ]
    }
  ],
  "fieldConfig": {
    "defaults": {},
    "overrides": [
      {
        "matcher": { "id": "byName", "options": "Lag" },
        "properties": [
          { "id": "custom.cellOptions", "value": { "type": "color-background" } },
          {
            "id": "thresholds",
            "value": {
              "mode": "absolute",
              "steps": [
                { "color": "green", "value": null },
                { "color": "orange", "value": 100 },
                { "color": "red", "value": 1000 }
              ]
            }
          }
        ]
      }
    ]
  }
}
```

Replace the URL with the address of your gateway, and adjust the lag thresholds to your workload. Since the array is at the root of the response, `root_selector` is left empty. If the API requires authentication, configure it on the Infinity data source rather than in the panel.
