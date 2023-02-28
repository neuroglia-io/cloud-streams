fromStream('cloud-events')
    .when({
        $init: function () {
            return {
                subjects: []
            }
        },
        $any: function (s, e) {
            if (!e.metadataRaw || e.metadataRaw.length < 1) return;
            var metadata = JSON.parse(e.metadataRaw);
            var subject = metadata.subject;
            if (!subject || subject.length < 1 || s.subjects.includes(subject)) return;
            s.subjects.push(subject);
        }
    })