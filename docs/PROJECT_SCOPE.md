<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# GRELECY — Current project scope / Périmètre actuel

Updated / Mis à jour : **2026-10-31**.

## English

**GRELECY — Green Legacy Community** is an independent community bringing together GPU owners, enthusiasts, testers, researchers, and developers to explore ways to extend the useful life of older graphics hardware on Windows.

### Initial community scope

| Architecture | Initial examples |
| --- | --- |
| Kepler | Kepler-based GTX 600/700 models |
| Maxwell | Maxwell-based GTX 900 models |
| Pascal | Pascal-based GeForce GTX 10 series: GTX 1050, 1050 Ti, 1060, 1070, 1070 Ti, 1080, 1080 Ti |
| Turing (GTX 16 only) | GeForce GTX 1630, GTX 1650 and GTX 1660 families; SUPER, Ti and laptop variants where applicable |

**All four hardware groups are included from the start.** The initial scope includes Kepler, Maxwell, Pascal, and Turing-based GeForce GTX 16 cards. The Turing addition is limited to the GTX 16 series; RTX 20 and other Turing products are not automatically included. Community inclusion does not imply that official support has ended for every listed GPU. This scope change aims to welcome a wider pool of owners and contributors.

These examples are not an exhaustive hardware whitelist. Record the exact GPU, desktop or laptop variant, architecture where known, Windows version and build, and driver version. A product series does not always identify its architecture. Reports involving other models can be useful when the hardware is clearly identified.

A GTX 1060 Pascal is available to the project initiator for tests and now falls within the initial community scope. Other owners and contributors are welcome, including those without a target GPU.

### Stage and method

The project is forming its community and gathering hardware information, research, and reproducible problems. Technical goals will be chosen collectively from the evidence, hardware, and skills available. The order in the table is not a development priority or release plan.

The [AI and gaming research roadmap](AI_GAMING_ROADMAP.md) proposes bounded experiments within this scope, starting with the available GTX 1060 and expanding according to hardware and contributor availability. Its gates guide evidence collection; they do not change community membership, establish support or commit to release dates. Firmware feasibility reviews remain separate from software experiments.

GRELECY does not currently provide drivers, patches, or supported software releases. Inclusion in the community scope does not promise new drivers, performance improvements, API support, security fixes, or compatibility for an individual GPU.

The public starting point is [the GitHub repository](https://github.com/FaipL/Grelecy). Use the [introduction form](https://github.com/FaipL/Grelecy/issues/new?template=01-join.yml), [compatibility report](https://github.com/FaipL/Grelecy/issues/new?template=02-compatibility.yml), and [research proposal form](https://github.com/FaipL/Grelecy/issues/new?template=03-proposal.yml).

## Français

**GRELECY — Green Legacy Community** est une communauté indépendante qui réunit propriétaires de GPU, passionnés, testeurs, chercheurs et développeurs afin d’explorer comment prolonger l’utilisation des anciennes cartes graphiques sous Windows.

### Périmètre initial de la communauté

| Architecture | Exemples de départ |
| --- | --- |
| Kepler | Modèles GTX 600/700 fondés sur Kepler |
| Maxwell | Modèles GTX 900 fondés sur Maxwell |
| Pascal | GeForce GTX série 10 fondées sur Pascal : GTX 1050, 1050 Ti, 1060, 1070, 1070 Ti, 1080, 1080 Ti |
| Turing (GTX série 16 uniquement) | Familles GeForce GTX 1630, GTX 1650 et GTX 1660 ; variantes SUPER, Ti et portables lorsqu’elles existent |

**Les quatre groupes de matériel sont inclus dès le départ.** Le périmètre initial comprend Kepler, Maxwell, Pascal et les GeForce GTX série 16 fondées sur Turing. Cet ajout de Turing porte uniquement sur les GTX série 16 ; les RTX 20 et les autres produits Turing ne sont pas inclus automatiquement. L’inclusion dans la communauté ne signifie pas que le support officiel est terminé pour chaque GPU listé. L’objectif de cet élargissement est de toucher davantage de propriétaires et de contributeurs.

Cette liste d’exemples n’est pas une liste exhaustive des cartes acceptées. Préciser le GPU exact, sa variante de bureau ou portable, son architecture si elle est connue, la version et le build de Windows ainsi que la version du pilote. Une série commerciale ne suffit pas toujours à déterminer l’architecture. Les retours sur d’autres modèles peuvent être utiles lorsque le matériel est clairement identifié.

Une GTX 1060 Pascal est disponible auprès de l’initiateur du projet pour les tests et relève désormais du périmètre initial. Les autres propriétaires et contributeurs sont bienvenus, y compris sans GPU ciblé.

### État et méthode

Le projet est en phase de constitution de communauté, de recherche et de collecte des configurations et problèmes reproductibles. Les objectifs techniques seront choisis collectivement en fonction des observations, du matériel et des compétences disponibles. L’ordre du tableau ne définit ni priorité de développement ni calendrier de livraison.

La [roadmap de recherche IA et jeux vidéo](AI_GAMING_ROADMAP.md) propose des expériences limitées dans ce périmètre, en commençant par la GTX 1060 disponible puis selon le matériel et les volontaires réunis. Ses étapes organisent la collecte de preuves sans modifier l’accueil dans la communauté, garantir une prise en charge ou engager des dates de livraison. Les études de faisabilité du firmware restent distinctes des essais logiciels.

Aucun pilote, correctif ou logiciel pris en charge par GRELECY n’est actuellement disponible. L’inclusion d’un GPU dans le périmètre ne garantit pas de nouveau pilote, de gain de performance, de nouvelles API, de correctif de sécurité ou de compatibilité pour cette carte.

### Référence pour les instructions du projet

- Vision : créer une communauté internationale indépendante pour prolonger l’utilisation des anciennes cartes graphiques sous Windows.
- Périmètre initial : **Kepler, Maxwell, Pascal et GTX série 16 Turing**, notamment les GTX 600/700 Kepler, GTX 900 Maxwell, GeForce GTX série 10 Pascal et GeForce GTX série 16 Turing. Toujours vérifier le modèle et l’architecture.
- Ressource de test connue : une GTX 1060 Pascal, incluse dans le périmètre initial.
- Approche : réunir d’abord les passionnés, propriétaires, testeurs et développeurs, puis définir ensemble les objectifs techniques.
- Ne pas présupposer le développement de nouveaux pilotes ni présenter une piste technique comme une décision validée.
- Répondre en français dans les échanges de préparation ; privilégier l’anglais pour les communications internationales.
- Distinguer résultats observés, hypothèses et engagements. Tenir compte des contraintes des pilotes propriétaires et de la signature Windows.
- Identité : GRELECY, développé Green Legacy Community, direction « Hardware Legacy ».

## Related documents / Documents associés

- [README](../README.md)
- [Contributing](../CONTRIBUTING.md)
- [AI and gaming research roadmap / Roadmap de recherche IA et jeux vidéo](AI_GAMING_ROADMAP.md)
- [English and French recruitment drafts / Textes de recrutement anglais et français](RECRUITMENT.md)
- [Website editing guide / Guide du site](../website/README.md)
- [Brand policy / Politique de marque](../BRANDING.md)

Hardware naming reference / Référence pour la série Pascal : [NVIDIA — GeForce GTX 10 series](https://www.nvidia.com/en-us/geforce/products/). [NVIDIA — GeForce GTX 16 series comparison](https://www.nvidia.com/en-us/geforce/graphics-cards/compare/). The project scope is a GRELECY decision and does not imply NVIDIA endorsement or driver availability.
