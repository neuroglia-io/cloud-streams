const baseConfig = {
    d3,
    zoom: {
        minimumScale: 1,
        restrictPan: true,
    },
    drop: {
        date: d => new Date(d.date)
    }
};

export function renderTimeline(el, dataset, start, end) {
    const chart = eventDrops({
        ...baseConfig,
        range: {
            start: new Date(start),
            end: new Date(end)
        }
    });
    d3
        .select(el)
        .data([dataset])
        .call(chart);
}