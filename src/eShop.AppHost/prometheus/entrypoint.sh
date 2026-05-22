#!/bin/sh
# Template'deki env değişkenlerini gerçek değerlerle doldur
envsubst < /etc/prometheus/prometheus.template.yml > /etc/prometheus/prometheus.yml
# Prometheus'u başlat
exec prometheus "$@"
