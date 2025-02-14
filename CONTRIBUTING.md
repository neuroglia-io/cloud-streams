﻿# Contributing to Cloud Streams

Thank you for considering contributing to **Cloud Streams**! 🎉 Your contributions help improve the project and ensure it remains a powerful **event-driven projections database**.  

Please follow the guidelines below to ensure a smooth and effective contribution process.

---

## 🚀 How to Contribute

### **🐞 Reporting Issues**
If you encounter any **bugs**, have **suggestions**, or want to **report a security vulnerability**, please use **GitHub Issues**:

1. Go to the [Issues page](https://github.com/neuroglia-io/cloud-streams/issues).
2. **Search for existing issues** to avoid duplicates.
3. If the issue hasn't been reported, click **"New issue"**.
4. Provide a **clear and concise description** of the problem or suggestion.
5. Include relevant details such as **logs, error messages, or screenshots**.

---

### **💡 Suggesting Enhancements**
If you have **ideas for new features or improvements**, please:

1. Open a new issue on the [Issues page](https://github.com/neuroglia-io/cloud-streams/issues).
2. **Clearly describe** the enhancement or feature request.
3. Explain **why it would be valuable** and any potential **use cases**.

---

### **📥 Submitting Pull Requests (PRs)**

To **contribute code changes**, follow these steps:

#### **1️⃣ Fork & Clone the Repository**
- Click **"Fork"** on GitHub to create a personal copy of the repository.
- Clone your fork locally:
  
    ```sh
    git clone https://github.com/<your-username>/cloud-streams.git
    cd cloud-streams
    ```

- Set up the **upstream repository** to track changes from the main repo:
  
    ```sh
    git remote add upstream https://github.com/neuroglia-io/cloud-streams.git
    ```

#### **2️⃣ Create a Branch**
- Always create a **new branch** for your contributions:

    ```sh
    git checkout -b feature/my-new-feature
    ```

- Use a **descriptive branch name**, such as:
  - `fix/event-validation`
  - `feature/workflow-indexing`
  - `chore/docs-update`

#### **3️⃣ Implement & Test Changes**
- Make sure your code follows the **project coding standards**.
- Add or update **unit tests** if applicable.
- Run tests before committing:

    ```sh
    dotnet test
    ```

#### **4️⃣ Commit & Push Changes**
- Format commit messages as:

    ```sh
    git commit -m "fix(event-processing): Ensure CloudEvents are properly validated"
    ```

- Push your branch:

    ```sh
    git push origin feature/my-new-feature
    ```

#### **5️⃣ Open a Pull Request**
- Go to **your fork** on GitHub and click **"New Pull Request"**.
- Select the **`main`** branch as the base and your **feature branch** as the compare branch.
- Provide a **detailed description** of your changes.
- Tag relevant contributors or reviewers.

---

## **🔍 Code Style Guidelines**
To maintain consistency, follow these **coding standards**:

✅ Use **C# conventions** and follow `.editorconfig` rules.  
✅ Keep methods **small and modular** for readability.  
✅ Use **meaningful variable and function names**.  
✅ Always include **XML documentation comments** for public methods.  

---

## **✅ Contribution Checklist**
Before submitting a PR, make sure you have:

☑ **Tested** your changes locally.  
☑ **Updated documentation** if applicable.  
☑ **Followed coding style guidelines**.  
☑ **Linked the issue** your PR addresses (if applicable).  
☑ **Checked that CI/CD pipelines pass** after pushing the PR.  

---

## **📜 License & Contributor Agreement**
By contributing to **Cloud Streams**, you agree that your contributions will be licensed under the [Apache-2.0 License](LICENSE).

---

## **📩 Need Help?**
If you have questions, feel free to:

💬 **Ask in Discussions** – [GitHub Discussions](https://github.com/neuroglia-io/cloud-streams/discussions)  
🐞 **Report an Issue** – [GitHub Issues](https://github.com/neuroglia-io/cloud-streams/issues)  

Thank you for making **Cloud Streams** better! 🚀  
