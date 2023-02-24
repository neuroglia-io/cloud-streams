const byType = 'by-type';
const bySource = 'by-source';
const bySubject = 'by-subject';

const updatePartition = (stream, type, id, created) => {
    if (stream[type].keys.includes(id)) {
        stream[type].values[id].lastEvent = created;
        stream[type].values[id].length++;
        return;
    }
    stream[type].keys.push(id);
    stream[type].values[id] = {
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
        },
        $any: function (stream, evt) {
            if (!evt || !evt.metadataRaw || !evt.metadataRaw) return;
            const metadata = JSON.parse(evt.metadataRaw);
            const type = metadata.type;
            const source = metadata.source;
            const subject = metadata.subject;
            const created = metadata.time;
            if (type && type.length) {
                updatePartition(stream, byType, type, created);
            }
            if (source && source.length) {
                updatePartition(stream, bySource, source, created);
            }
            if (subject && subject.length) {
                updatePartition(stream, bySubject, subject, created);
            }
        }
    });