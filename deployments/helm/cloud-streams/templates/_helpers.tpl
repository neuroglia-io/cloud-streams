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

{{/*
The instances of a component, 'gateway' or 'broker', as a JSON object whose 'items' are the values of each instance: pass a dict with the root context and
the component's name. Each item of the 'gateways' or 'brokers' list overrides the settings of 'gateway' or 'broker', which a single instance uses when the list
is empty. Every instance serves one gateway or broker resource of a namespace (see the README's 'Namespaces' section), and is given:
  - 'name' and 'namespace': the name and the namespace of the resource it serves
  - 'objectName': the name of its Kubernetes objects
  - 'instanceLabel': the value of the label that tells its pods apart from the other instances' ones, if there are several
*/}}
{{- define "cloud-streams.instances" -}}
{{- $root := .root }}
{{- $component := .component }}
{{- $defaults := index $root.Values $component }}
{{- $items := index $root.Values (printf "%ss" $component) | default list }}
{{- $fullname := include "cloud-streams.fullname" $root }}
{{- $instances := list }}
{{- if $items }}
{{- $names := dict }}
{{- range $index, $item := $items }}
{{- $values := mergeOverwrite (deepCopy $defaults) (deepCopy $item) }}
{{- $name := required (printf "%ss[%d].name is required" $component $index) $item.name }}
{{- $namespace := $item.namespace | default "default" }}
{{- $key := printf "%s/%s" $namespace $name }}
{{- if hasKey $names $key }}{{ fail (printf "%ss[%d]: the %s '%s' is listed more than once" $component $index $component $key) }}{{ end }}
{{- $_ := set $names $key true }}
{{- $_ := set $values "name" $name }}
{{- $_ := set $values "namespace" $namespace }}
{{- $_ := set $values "objectName" ($item.fullnameOverride | default (printf "%s-%s-%s-%s" $fullname $component $namespace $name | trunc 63 | trimSuffix "-")) }}
{{- $_ := set $values "instanceLabel" (printf "%s.%s" $namespace $name | trunc 63 | trimSuffix "-" | trimSuffix ".") }}
{{- $instances = append $instances $values }}
{{- end }}
{{- else }}
{{- $values := deepCopy $defaults }}
{{- $_ := set $values "name" ($defaults.name | default (printf "%s-%s" $fullname $component)) }}
{{- $_ := set $values "namespace" ($defaults.namespace | default "default") }}
{{- $_ := set $values "objectName" (printf "%s-%s" $fullname $component) }}
{{- $_ := set $values "instanceLabel" "" }}
{{- $instances = append $instances $values }}
{{- end }}
{{- toJson (dict "items" $instances) }}
{{- end }}

{{/* The selector labels of an instance of a component: pass a dict with the root context, the component's name and the instance's values */}}
{{- define "cloud-streams.instanceSelectorLabels" -}}
{{- include "cloud-streams.selectorLabels" (dict "root" .root "component" .component) }}
{{- with .instance.instanceLabel }}
cloud-streams.io/{{ $.component }}: {{ . | quote }}
{{- end }}
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

{{/* The settings of the providers secrets are read from (see the README's 'Transports' section) */}}
{{- define "cloud-streams.secretsEnv" -}}
{{- range $index, $provider := .Values.secrets.providers }}
- name: CloudStreams__Secrets__Providers__{{ $index }}
  value: {{ $provider | quote }}
{{- end }}
{{- range $index, $namespace := .Values.secrets.sharedWith }}
- name: CloudStreams__Secrets__SharedWith__{{ $index }}
  value: {{ $namespace | quote }}
{{- end }}
{{- if has "vault" .Values.secrets.providers }}
{{- with .Values.secrets.vault }}
- name: CloudStreams__Secrets__Vault__Address
  value: {{ required "secrets.vault.address is required by the 'vault' secret provider" .address | quote }}
- name: CloudStreams__Secrets__Vault__Mount
  value: {{ .mount | quote }}
- name: CloudStreams__Secrets__Vault__KvVersion
  value: {{ .kvVersion | quote }}
{{- with .prefix }}
- name: CloudStreams__Secrets__Vault__Prefix
  value: {{ . | quote }}
{{- end }}
- name: CloudStreams__Secrets__Vault__Authentication__Method
  value: {{ .authentication.method | quote }}
{{- if eq .authentication.method "kubernetes" }}
- name: CloudStreams__Secrets__Vault__Authentication__Role
  value: {{ .authentication.role | quote }}
{{- else if eq .authentication.method "token" }}
- name: CloudStreams__Secrets__Vault__Authentication__Token
  valueFrom:
    secretKeyRef:
      name: {{ required "secrets.vault.authentication.existingSecret is required by the token method" .authentication.existingSecret }}
      key: token
{{- else if eq .authentication.method "approle" }}
- name: CloudStreams__Secrets__Vault__Authentication__RoleId
  valueFrom:
    secretKeyRef:
      name: {{ required "secrets.vault.authentication.existingSecret is required by the approle method" .authentication.existingSecret }}
      key: roleId
- name: CloudStreams__Secrets__Vault__Authentication__SecretId
  valueFrom:
    secretKeyRef:
      name: {{ .authentication.existingSecret }}
      key: secretId
{{- end }}
{{- end }}
{{- end }}
{{- end }}

{{/* Whether pods need the token of their service account: to read Kubernetes secrets, or to authenticate to Vault with Kubernetes */}}
{{- define "cloud-streams.needsServiceAccountToken" -}}
{{- if or (include "cloud-streams.kubernetesMode" .) (has "kubernetes" .Values.secrets.providers) (and (has "vault" .Values.secrets.providers) (eq .Values.secrets.vault.authentication.method "kubernetes")) }}true{{ else }}false{{ end }}
{{- end }}

{{/* The volumes of the Kubernetes secrets the 'file' provider reads */}}
{{- define "cloud-streams.secretVolumes" -}}
{{- range .Values.secrets.files }}
- name: secret-{{ .name }}
  secret:
    secretName: {{ .secretName | default .name }}
{{- end }}
{{- end }}

{{/* The mounts of the Kubernetes secrets the 'file' provider reads */}}
{{- define "cloud-streams.secretVolumeMounts" -}}
{{- range .Values.secrets.files }}
- name: secret-{{ .name }}
  mountPath: /var/run/secrets/cloud-streams/{{ .name }}
  readOnly: true
{{- end }}
{{- end }}

{{/* Whether resources are Kubernetes custom resources, which the operator deploys gateways and brokers from (see the README's 'Kubernetes-native mode' section) */}}
{{- define "cloud-streams.kubernetesMode" -}}
{{- if eq (.Values.resources.store | default "redis") "kubernetes" }}true{{ end }}
{{- end }}

{{/* The Kubernetes namespace of the 'default' namespace of Cloud Streams */}}
{{- define "cloud-streams.defaultNamespace" -}}
{{- .Values.resources.kubernetes.defaultNamespace | default .Release.Namespace }}
{{- end }}

{{/* The Kubernetes namespace of a namespace of Cloud Streams: pass a dict with the root context and the namespace */}}
{{- define "cloud-streams.kubernetesNamespace" -}}
{{- if eq .namespace "default" }}{{ include "cloud-streams.defaultNamespace" .root }}{{ else }}{{ .namespace }}{{ end }}
{{- end }}

{{/*
The instances of a component the chart deploys itself: those of 'cloud-streams.instances', except in Kubernetes mode, where the operator deploys gateways
and brokers, and the chart only deploys the API, a gateway with the settings of 'gateway' that 'api' overrides
*/}}
{{- define "cloud-streams.deployedInstances" -}}
{{- if include "cloud-streams.kubernetesMode" .root }}
{{- $instances := list }}
{{- if eq .component "gateway" }}
{{- $values := mergeOverwrite (deepCopy .root.Values.gateway) (omit (deepCopy .root.Values.api) "namespaces") }}
{{- $fullname := include "cloud-streams.fullname" .root }}
{{- $_ := set $values "enabled" true }}
{{- $_ := set $values "name" (.root.Values.api.name | default (printf "%s-api" $fullname)) }}
{{- $_ := set $values "namespace" (.root.Values.api.namespace | default "default") }}
{{- $_ := set $values "objectName" (printf "%s-api" $fullname | trunc 63 | trimSuffix "-") }}
{{- $_ := set $values "instanceLabel" "api" }}
{{- $_ := set $values "managementApi" (dict "enabled" true "port" "") }}
{{- $instances = append $instances $values }}
{{- end }}
{{- toJson (dict "items" $instances) }}
{{- else }}
{{- include "cloud-streams.instances" . }}
{{- end }}
{{- end }}

{{/* The environment variables of the processes the chart deploys in Kubernetes mode, which store resources as custom resources */}}
{{- define "cloud-streams.kubernetesEnv" -}}
{{- if include "cloud-streams.kubernetesMode" . }}
- name: CloudStreams__Resources__Store
  value: kubernetes
- name: CloudStreams__Resources__Kubernetes__DefaultNamespace
  value: {{ include "cloud-streams.defaultNamespace" . | quote }}
{{- range $index, $namespace := .Values.api.namespaces }}
- name: CloudStreams__Resources__Kubernetes__Namespaces__{{ $index }}
  value: {{ $namespace | quote }}
{{- end }}
# the secrets of a namespace are those of its Kubernetes namespace, and the shared ones those of the release's namespace
- name: CloudStreams__Secrets__Kubernetes__NamespacedSecrets
  value: "true"
- name: CloudStreams__Secrets__Kubernetes__DefaultNamespace
  value: {{ include "cloud-streams.defaultNamespace" . | quote }}
- name: CloudStreams__Secrets__Kubernetes__Namespace
  value: {{ .Release.Namespace | quote }}
# the namespace of the data-protection keys, and the identity of the replica in the leases
- name: POD_NAMESPACE
  valueFrom:
    fieldRef:
      fieldPath: metadata.namespace
- name: POD_NAME
  valueFrom:
    fieldRef:
      fieldPath: metadata.name
{{- end }}
{{- end }}

{{/* The plural names of the kinds of Cloud Streams */}}
{{- define "cloud-streams.kinds" -}}
[ brokers, feeds, gateways, schemas, sinks, subscriptions ]
{{- end }}
