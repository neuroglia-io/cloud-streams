{{/* The name of the chart */}}
{{- define "cloud-streams.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/* The fully qualified name of the release's resources */}}
{{- define "cloud-streams.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/* The labels of every resource */}}
{{- define "cloud-streams.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
app.kubernetes.io/name: {{ include "cloud-streams.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Values.image.tag | default .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/* The selector labels of a component: pass a dict with the root context and the component's name */}}
{{- define "cloud-streams.selectorLabels" -}}
app.kubernetes.io/name: {{ include "cloud-streams.name" .root }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .component }}
{{- end }}

{{/* The image of a component: pass a dict with the root context and the component's name */}}
{{- define "cloud-streams.image" -}}
{{- printf "%s/%s-%s:%s" .root.Values.image.registry .root.Values.image.repositoryPrefix .component (.root.Values.image.tag | default .root.Chart.AppVersion) }}
{{- end }}

{{/* The name of the service account */}}
{{- define "cloud-streams.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "cloud-streams.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/* The name of the secret that holds the connection strings */}}
{{- define "cloud-streams.secretName" -}}
{{- printf "%s-connections" (include "cloud-streams.fullname" .) }}
{{- end }}

{{/* The name of the gateway resource */}}
{{- define "cloud-streams.gatewayName" -}}
{{- .Values.gateway.name | default (printf "%s-gateway" (include "cloud-streams.fullname" .)) }}
{{- end }}

{{/* The name of the broker resource */}}
{{- define "cloud-streams.brokerName" -}}
{{- .Values.broker.name | default (printf "%s-broker" (include "cloud-streams.fullname" .)) }}
{{- end }}

{{/* The environment variables shared by the gateway and the broker */}}
{{- define "cloud-streams.commonEnv" -}}
- name: ConnectionStrings__eventstore
  valueFrom:
    secretKeyRef:
      name: {{ .Values.eventstore.existingSecret | default (include "cloud-streams.secretName" .) }}
      key: {{ if .Values.eventstore.existingSecret }}{{ .Values.eventstore.existingSecretKey }}{{ else }}eventstore{{ end }}
- name: ConnectionStrings__redis
  valueFrom:
    secretKeyRef:
      name: {{ .Values.redis.existingSecret | default (include "cloud-streams.secretName" .) }}
      key: {{ if .Values.redis.existingSecret }}{{ .Values.redis.existingSecretKey }}{{ else }}redis{{ end }}
{{- with .Values.telemetry.otlpEndpoint }}
- name: OTEL_EXPORTER_OTLP_ENDPOINT
  value: {{ . | quote }}
{{- end }}
{{- end }}

{{/* The probes of a component, on its health endpoints: pass a dict with the root context and the port */}}
{{- define "cloud-streams.probes" -}}
livenessProbe:
  httpGet:
    path: /healthz/live
    port: {{ .port }}
  {{- toYaml .root.Values.probes.liveness | nindent 2 }}
readinessProbe:
  httpGet:
    path: /healthz
    port: {{ .port }}
  {{- toYaml .root.Values.probes.readiness | nindent 2 }}
{{- end }}
