import { $, chalk } from 'npm:zx';

const containerAppName = Deno.env.get('NAME');
const containerName = Deno.env.get('CONTAINER_NAME');
const resourceGroup = Deno.env.get('RESOURCE_GROUP');
const image = Deno.env.get('IMAGE');

const latestRevision = await updateContainerImage();
console.log(`latestRevision: ${chalk.cyan(latestRevision)}`);

let healthy = false;
for (let i = 0; i < 5; i++) {
  const healthState = await getRevisionHealthState(latestRevision);
  console.log(`healthState: ${chalk.cyan(healthState)}`);
  if (healthState === 'Healthy') {
    console.log('Health state is healthy');
    healthy = true;
    break;
  }

  console.log(`Not healty yet (${i}), waiting 30 seconds...`);
  await new Promise((resolve) => setTimeout(resolve, 30_000));
}

if (!healthy) {
  console.log('Health state is not healthy');
  Deno.exit(1);
}

async function updateContainerImage() {
  const output = await $`az containerapp update \
    --name ${containerAppName} \
    --resource-group ${resourceGroup} \
    --container-name ${containerName} \
    --image ${image}`.quiet();
  const parsed = JSON.parse(output.stdout);
  // console.log(parsed);
  return parsed.properties.latestRevisionName;
}

async function getRevisionHealthState(revisionName: string) {
  const output = await $`az containerapp revision show \
    --name ${containerAppName} \
    --resource-group ${resourceGroup} \
    --revision ${revisionName}`.quiet();
  const parsed = JSON.parse(output.stdout);
  return parsed.properties.healthState;
}
