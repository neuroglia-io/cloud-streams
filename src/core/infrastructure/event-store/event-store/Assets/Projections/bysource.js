fromStream('cloud-events')
    .when({
        $any: (_, evt) => {
            if (!evt || !evt.metadataRaw) return;
            const metadata = JSON.parse(evt.metadataRaw);
            if (!metadata || !metadata.contextAttributes || !metadata.contextAttributes.source) return;
            linkTo('$by-source-' + metadata.contextAttributes.source, evt);
        }
    });