package com.estacionamento.parking.offline

class OfflineQueueStore(private val persistence: OfflineQueuePersistence) {

    private fun load(): MutableList<OfflineQueueItem> =
        OfflineQueueJson.fromJson(persistence.loadJson()).toMutableList()

    private fun save(items: List<OfflineQueueItem>) {
        persistence.saveJson(OfflineQueueJson.toJson(items))
    }

    @Synchronized
    fun enqueue(item: OfflineQueueItem) {
        val q = load()
        q.add(item)
        save(q)
    }

    @Synchronized
    fun peekOrNull(): OfflineQueueItem? = load().firstOrNull()

    @Synchronized
    fun removeHead() {
        val q = load()
        if (q.isNotEmpty()) {
            q.removeAt(0)
            save(q)
        }
    }

    @Synchronized
    fun updateHeadAttempts(attempts: Int) {
        val q = load()
        if (q.isEmpty()) return
        val h = q[0]
        q[0] = h.copy(attempts = attempts)
        save(q)
    }

    @Synchronized
    fun size(): Int = load().size
}
