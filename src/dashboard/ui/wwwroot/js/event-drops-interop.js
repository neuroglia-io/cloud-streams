const baseConfig = {
    d3,
    zoom: {
        minimumScale: 1,
        restrictPan: true,
    },
    drop: {
        date: d => new Date(d.time)
    }
};

export function renderTimeline(el, dotnetRef, dataset, start, end) {
    const chart = eventDrops({
        ...baseConfig,
        range: {
            start: new Date(start),
            end: new Date(end)
        },
        drop: {
            ...baseConfig.drop,
            onMouseOver: async (e, cloudEvent) => await dotnetRef.invokeMethodAsync("ShowTooltipOnMouseOver", cloudEvent, e.pageX, e.pageY),
            onMouseOut: async () => await dotnetRef.invokeMethodAsync("HideTooltipOnMouseOut")
        }
    });
    d3
        .select(el)
        .data([dataset])
        .call(chart);
}