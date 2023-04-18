fromStream('cloud-events')
    .when({
        $any: (_, evt) => {
            if (!evt || !evt.metadataRaw) return;
            const metadata = JSON.parse(evt.metadataRaw);
            if (!metadata || !metadata.contextAttributes || !metadata.contextAttributes.subject) return;
            linkTo('$by-subject-' + metadata.contextAttributes.subject, evt);
        }
    });