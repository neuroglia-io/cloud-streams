const byType = 'ByType';
const bySource = 'BySource';
const bySubject = 'BySubject';

const updatePartitionMetadata = (stream, type, id, created) => {
    if (stream.entries[type].keys.includes(id)) {
        stream.entries[type].values[id].lastEvent = created;
        stream.entries[type].values[id].length++;
        return;
    }
    stream.entries[type].keys.push(id);
    stream.entries[type].values[id] = {
        id,
        type,
        firstEvent: created,
        lastEvent: created,
        length: 1
    };
}

fromStream('cloud-events')
    .when({
        $init: function () {
            return {
                entries: {
                    [byType]: {
                        keys: [],
                        values: {}
                    },
                    [bySource]: {
                        keys: [],
                        values: {}
                    },
                    [bySubject]: {
                        keys: [],
                        values: {}
                    }
                }
            }
        },
        $any: function (stream, evt) {
            if (!evt || !evt.metadataRaw || !evt.metadataRaw) return;
            const metadata = JSON.parse(evt.metadataRaw);
            const type = metadata.type;
            const source = metadata.source;
            const subject = metadata.subject;
            const created = metadata.time;
            if (type && type.length) {
                updatePartitionMetadata(stream, byType, type, created);
            }
            if (source && source.length) {
                updatePartitionMetadata(stream, bySource, source, created);
            }
            if (subject && subject.length) {
                updatePartitionMetadata(stream, bySubject, subject, created);
            }
        }
    });