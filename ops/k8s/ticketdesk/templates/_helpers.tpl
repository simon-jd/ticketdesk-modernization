{{- define "td.labels" -}}
app.kubernetes.io/part-of: ticketdesk
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end -}}

{{- define "td.image" -}}
{{- $root := index . 0 -}}{{- $name := index . 1 -}}
{{- if $root.Values.image.registry -}}{{ $root.Values.image.registry }}/{{ end -}}{{ $name }}:{{ $root.Values.image.tag }}
{{- end -}}

{{/* ConnectionStrings__TicketDesk: a plain value (no secret inside, Entra auth) or a secret reference (kind, SQL login) */}}
{{- define "td.dbEnv" -}}
{{- $db := index . 0 -}}{{- $name := index . 1 -}}
- name: {{ $name }}
{{- if $db.connectionString }}
  value: {{ $db.connectionString | quote }}
{{- else }}
  valueFrom:
    secretKeyRef: { name: {{ required "database secretName or connectionString" $db.secretName | quote }}, key: {{ $db.secretKey | quote }} }
{{- end }}
{{- end -}}

{{- define "td.azureEnv" -}}
{{- if .Values.azure.workloadIdentity }}
# Azure.Identity: the federated token the workload identity webhook projects, nothing else of the credential chain
- name: AZURE_TOKEN_CREDENTIALS
  value: WorkloadIdentityCredential
{{- end }}
{{- end -}}

{{- define "td.podLabels" -}}
{{- if .Values.azure.workloadIdentity }}
azure.workload.identity/use: "true"
{{- end }}
{{- end -}}
