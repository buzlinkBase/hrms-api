1. How to add a Submodule
Run this command from the root of your main repository:

git submodule add <repository-url> <path/to/folder>

Example based on your project:
If you wanted to add the tenantstore library as a submodule:
git submodule add [https://github.com/user/tenantstore.git](https://github.com/user/tenantstore.git) tenantstore

2. After adding it
When you add a submodule, Git creates a .gitmodules file. You must commit this file and the new folder to your main repo:

git add .

git commit -m "Add tenantstore as a submodule"

git push origin main

3. How to Clone a Repo with Submodules
If a teammate (or your build server) clones your repo, the submodule folders will be empty by default. They need to run:

git clone --recursive <main-repo-url>

OR, if they already cloned it:
git submodule update --init --recursive

4. How to Update Submodules
Submodules are "pinned" to a specific commit. If the code in the sub-repository changes and you want the latest version:

git submodule update --remote --merge