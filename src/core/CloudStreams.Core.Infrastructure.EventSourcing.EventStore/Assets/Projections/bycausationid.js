fromStream('cloud-events')
    .when({
        $any: (_, evt) => {
            if (!evt || !evt.metadataRaw) return;
            const metadata = JSON.parse(evt.metadataRaw);
            if (!metadata || !metadata.$causationId) return;
            linkTo('$by-causation-' + metadata.$causationId, evt);
        }
    });