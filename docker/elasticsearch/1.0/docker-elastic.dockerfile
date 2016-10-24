FROM elasticsearch:2.2
RUN /usr/share/elasticsearch/bin/plugin install mapper-attachments
CMD elasticsearch -Des.network.host=0.0.0.0 -Dmax_thread_count=1 -Dindices.breaker.fielddata.limit=85% -Dindices.fielddata.cache.size=75% -Dindex.mapping.attachment.detect_language=true -Dindex.mapping.attachment.indexed_chars=-1 -Des.insecure.allow.root=true