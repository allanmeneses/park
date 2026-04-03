<template>
  <div class="page">
    <h1>Cadastro de lojistas</h1>
    <p class="lead">
      Como administrador do estacionamento, gere aqui os códigos para novos lojistas se registarem na app.
    </p>
    <MgrLojistaInvitesSection />
    <button type="button" class="btn-secondary" style="margin-top: 1.5rem" aria-label="Voltar" @click="$router.push('/gestor')">
      Voltar ao painel
    </button>
  </div>
</template>

<script setup lang="ts">
import MgrLojistaInvitesSection from '@/components/MgrLojistaInvitesSection.vue'
</script>

<style scoped>
.lead {
  color: #424242;
  margin-bottom: 1rem;
  max-width: 40rem;
}
</style>
